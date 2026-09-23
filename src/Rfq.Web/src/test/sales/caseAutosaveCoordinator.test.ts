import { describe, expect, it, vi } from 'vitest'
import { createCaseAutosaveCoordinator } from '@/pages/sales/caseAutosaveCoordinator'

interface State {
  version: number
  notional: number
  message: string
}

describe('Case autosave coordinator', () => {
  it('serializes rapid deltas and builds the next save from acknowledged state', async () => {
    let releaseFirst!: (state: State) => void
    const first = new Promise<State>((resolve) => {
      releaseFirst = resolve
    })
    const save = vi
      .fn<(state: State, delta: Partial<State>) => Promise<State>>()
      .mockReturnValueOnce(first)
      .mockImplementationOnce(async (state, delta) => ({
        ...state,
        ...delta,
        version: state.version + 1,
      }))
    const coordinator = createCaseAutosaveCoordinator<State, State>(
      { version: 3, notional: 100, message: '' },
      save,
    )

    const firstSave = coordinator.enqueue({ notional: 200 })
    const secondSave = coordinator.enqueue({ message: 'next' })
    expect(save).toHaveBeenCalledTimes(1)
    releaseFirst({ version: 4, notional: 200, message: '' })
    await Promise.all([firstSave, secondSave])

    expect(save).toHaveBeenNthCalledWith(
      2,
      { version: 4, notional: 200, message: '' },
      { message: 'next' },
    )
    expect(coordinator.acknowledged()).toEqual({
      version: 5,
      notional: 200,
      message: 'next',
    })
  })

  it('merges pending field intent while a save is in flight', async () => {
    let release!: (state: State) => void
    const save = vi
      .fn<(state: State, delta: Partial<State>) => Promise<State>>()
      .mockReturnValueOnce(
        new Promise((resolve) => {
          release = resolve
        }),
      )
      .mockImplementationOnce(async (state, delta) => ({
        ...state,
        ...delta,
        version: state.version + 1,
      }))
    const coordinator = createCaseAutosaveCoordinator<State, State>(
      { version: 1, notional: 100, message: '' },
      save,
    )

    const saving = coordinator.enqueue({ notional: 150 })
    void coordinator.enqueue({ message: 'a' })
    void coordinator.enqueue({ message: 'latest' })
    expect(coordinator.pending()).toEqual({ message: 'latest' })
    release({ version: 2, notional: 150, message: '' })
    await saving
    expect(save).toHaveBeenCalledTimes(2)
  })

  it('stops after a conflict and does not retry automatically', async () => {
    const conflict = { status: 409 }
    const save = vi.fn().mockRejectedValue(conflict)
    const coordinator = createCaseAutosaveCoordinator<State, State>(
      { version: 1, notional: 100, message: '' },
      save,
    )

    await expect(coordinator.enqueue({ notional: 200 })).rejects.toBe(conflict)
    expect(coordinator.stopped()).toBe(true)
    await expect(coordinator.enqueue({ message: 'later' })).rejects.toThrow(
      'Autosave stopped',
    )
    expect(save).toHaveBeenCalledOnce()
  })
})
