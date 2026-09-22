import { useState } from 'react'
import {
  useCommitPostProcessMutation,
  useGetPostProcessQuery,
  type PostProcessPreset,
  type PostProcessScope,
} from '@/services/api'
import { PostProcessScreen } from '@/features/post-process/PostProcessScreen'
import { useOutletContext } from 'react-router'
import type { AppOutletContext } from '@/app/App'

export function PostProcessWorkspace() {
  const { currentUserId } = useOutletContext<AppOutletContext>()
  const [preset, setPreset] = useState<PostProcessPreset>('Today')
  const [scope, setScope] = useState<PostProcessScope>('Mine')
  const query = useGetPostProcessQuery({ preset, scope })
  const [commit, commitState] = useCommitPostProcessMutation()

  return (
    <PostProcessScreen
      items={query.data ?? []}
      currentUserId={currentUserId}
      preset={preset}
      scope={scope}
      isLoading={query.isLoading || query.isFetching}
      isError={query.isError}
      isCommitting={commitState.isLoading}
      onPresetChange={setPreset}
      onScopeChange={setScope}
      onRefresh={() => query.refetch().then(() => undefined)}
      onCommit={(items) => commit({ items }).unwrap()}
    />
  )
}
