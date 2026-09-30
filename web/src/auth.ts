import { useQuery } from '@tanstack/react-query'
import { ApiError, api, type Me } from './api'

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

export const isAdmin = (me: Me | null | undefined) => me?.role === 'Admin' || me?.role === 'Supervisor'

export const MIN_PASSWORD = 10
