import type {
  GetPostProcessApiArg,
  PostProcessCommitItemRequest,
  PostProcessItemResponse,
  PostProcessLifecycleChangeValue,
} from '@/generated/rfqApi'

export type PostProcessPreset = NonNullable<GetPostProcessApiArg['preset']>
export type PostProcessScope = NonNullable<GetPostProcessApiArg['scope']>

export type PendingPostProcessChange = {
  caseId: number
  baseCurrentVersion: number
  lifecycle?: {
    type: PostProcessLifecycleChangeValue
    correctionReason?: string
  }
  memo?: {
    baseVersion: number
    value: string
  }
}

export function isCorrection(type: PostProcessLifecycleChangeValue) {
  return type === 'CorrectToHit' || type === 'CorrectToAway'
}

export function stagedState(change: PendingPostProcessChange | undefined) {
  switch (change?.lifecycle?.type) {
    case 'Hit':
    case 'CorrectToHit':
      return 'Hit'
    case 'Away':
    case 'CorrectToAway':
      return 'Away'
    case 'Cancel':
      return 'Cancelled'
    default:
      return undefined
  }
}

export function toCommitItem(
  change: PendingPostProcessChange,
): PostProcessCommitItemRequest {
  return {
    caseId: change.caseId,
    expectedCurrentVersion: change.baseCurrentVersion,
    lifecycleChange: change.lifecycle
      ? {
          type: change.lifecycle.type,
          correctionReason: change.lifecycle.correctionReason?.trim() || null,
        }
      : null,
    memoChange: change.memo
      ? {
          expectedMemoVersion: change.memo.baseVersion,
          value: change.memo.value,
        }
      : null,
  }
}

export function rowClass(
  row: PostProcessItemResponse,
  pending: boolean,
): string[] {
  const classes: string[] = []
  if (row.rfqStatus === 'Active' || row.rfqStatus === 'Presented')
    classes.push('post-process-row-unclosed')
  if (row.rfqStatus === 'Cancelled') classes.push('post-process-row-cancelled')
  if (pending) classes.push('post-process-row-pending')

  return classes
}
