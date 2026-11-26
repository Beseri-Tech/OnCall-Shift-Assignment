import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router'
import { ApiError, api, type Me } from './api'
import { notifyError } from './lib'

/** The signed-in account, or null when signed out. Any 401 elsewhere resets it (see main.tsx). */
export function useMe() {
  return useQuery({
    queryKey: ['me'],
    queryFn: async (): Promise<Me | null> => {
      try {
        return await api.auth.me()
      } catch (e) {
        if (e instanceof ApiError && e.status === 401) return null
        throw e
      }
    },
    staleTime: Infinity,
  })
}

export const isMaster = (me: Me | null | undefined) => me?.role === 'Master'
export const isAdmin = (me: Me | null | undefined) => me?.role === 'Admin' || me?.role === 'Supervisor' || isMaster(me)

export { safeNext } from './safeNext'

export const MIN_PASSWORD = 10

/** Master only: work in another hospital (or none). Everything cached belonged to the old one, so the cache is dropped. */
export function useSwitchFacility() {
  const qc = useQueryClient()
  const navigate = useNavigate()
  return useMutation({
    mutationFn: api.auth.setFacility,
    onSuccess: me => { qc.clear(); qc.setQueryData(['me'], me); navigate('/home') },
    onError: e => notifyError(e, 'Could not switch hospital'),
  })
}
