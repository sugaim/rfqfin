export interface CaseAutosaveCoordinator<State, Delta extends object> {
  enqueue: (delta: Partial<Delta>) => Promise<void>
  acknowledged: () => State
  pending: () => Partial<Delta>
  stopped: () => boolean
}

export function createCaseAutosaveCoordinator<State, Delta extends object>(
  initialState: State,
  save: (state: State, delta: Partial<Delta>) => Promise<State>,
): CaseAutosaveCoordinator<State, Delta> {
  let acknowledgedState = initialState
  let pendingDelta: Partial<Delta> = {}
  let active: Promise<void> | null = null
  let isStopped = false

  const drain = async () => {
    while (Object.keys(pendingDelta).length) {
      const delta = pendingDelta
      pendingDelta = {}
      try {
        acknowledgedState = await save(acknowledgedState, delta)
      } catch (error) {
        pendingDelta = { ...delta, ...pendingDelta }
        isStopped = true
        throw error
      }
    }
  }

  return {
    enqueue: (delta) => {
      if (isStopped)
        return Promise.reject(new Error('Autosave stopped after a failure.'))
      pendingDelta = { ...pendingDelta, ...delta }
      if (!active)
        active = drain().finally(() => {
          active = null
        })

      return active
    },
    acknowledged: () => acknowledgedState,
    pending: () => pendingDelta,
    stopped: () => isStopped,
  }
}
