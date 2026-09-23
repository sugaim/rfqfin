import { useState } from 'react'
import { Modal } from 'antd'
import {
  useCommitPostProcessChangesMutation,
  useGetPostProcessQuery,
  useGetGridConfigQuery,
  useSaveGridConfigMutation,
  type PostProcessCommitItemRequest,
} from '@/generated/rfqApi'
import { PostProcessScreen } from '@/pages/post-process/PostProcessScreen'
import type {
  PostProcessPreset,
  PostProcessScope,
} from '@/pages/post-process/postProcessModel'
import { unwrapApiResult } from '@/services/apiProblem'
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
  const [commit, commitState] = useCommitPostProcessChangesMutation()
  const [saveGridConfig] = useSaveGridConfigMutation()
  const blocker = useBlocker(hasPending)

  const commitChanges = async (items: PostProcessCommitItemRequest[]) => {
    const results = await unwrapApiResult(
      commit({ postProcessCommitRequest: { items } }),
    )
    await query
      .refetch()
      .unwrap()
      .catch(() => undefined)

    return results
  }

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
        onCommit={commitChanges}
        onPendingChange={setHasPending}
        gridConfigJson={
          gridConfigQuery.data
            ? JSON.stringify(gridConfigQuery.data.config)
            : undefined
        }
        onSaveGridConfig={(configJson) =>
          unwrapApiResult(
            saveGridConfig({
              screenId: 'post-process',
              configKey: 'main',
              gridConfigRequest: {
                version: (gridConfigQuery.data?.version ?? 0) + 1,
                config: JSON.parse(configJson),
              },
            }),
          ).then(async () => {
            await gridConfigQuery
              .refetch()
              .unwrap()
              .catch(() => undefined)
          })
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
