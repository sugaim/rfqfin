import { useState } from 'react'
import { Modal } from 'antd'
import {
  useCommitPostProcessMutation,
  useGetPostProcessQuery,
  useGetGridConfigQuery,
  useSaveGridConfigMutation,
  type PostProcessPreset,
  type PostProcessScope,
} from '@/services/api'
import { PostProcessScreen } from '@/features/post-process/PostProcessScreen'
import { useBlocker, useOutletContext } from 'react-router'
import type { AppOutletContext } from '@/app/App'

export function PostProcessWorkspace() {
  const { currentUserId } = useOutletContext<AppOutletContext>()
  const [preset, setPreset] = useState<PostProcessPreset>('Today')
  const [scope, setScope] = useState<PostProcessScope>('Mine')
  const [hasPending, setHasPending] = useState(false)
  const query = useGetPostProcessQuery({ preset, scope })
  const gridConfigQuery = useGetGridConfigQuery({
    screenId: 'post-process',
    configKey: 'main',
  })
  const [commit, commitState] = useCommitPostProcessMutation()
  const [saveGridConfig] = useSaveGridConfigMutation()
  const blocker = useBlocker(hasPending)

  return (
    <>
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
        onPendingChange={setHasPending}
        gridConfigJson={
          gridConfigQuery.data
            ? JSON.stringify(gridConfigQuery.data.config)
            : undefined
        }
        onSaveGridConfig={(configJson) =>
          saveGridConfig({
            screenId: 'post-process',
            configKey: 'main',
            version: (gridConfigQuery.data?.version ?? 0) + 1,
            config: JSON.parse(configJson),
          })
            .unwrap()
            .then(() => undefined)
        }
      />
      <Modal
        title="Discard uncommitted Post Process changes?"
        open={blocker.state === 'blocked'}
        okText="Discard and leave"
        cancelText="Stay"
        onOk={() => blocker.proceed?.()}
        onCancel={() => blocker.reset?.()}
      >
        Uncommitted Post Process changes will be discarded.
      </Modal>
    </>
  )
}
