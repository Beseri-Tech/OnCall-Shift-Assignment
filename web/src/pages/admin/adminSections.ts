import {
  IconCalendarCog, IconFileCertificate, IconFileText, IconMapPin, IconMessage2, IconPlaneDeparture, IconSum, IconUsers, IconWand,
} from '@tabler/icons-react'

/** Admin sections: the side nav lists them, AdminPage shows the one in the URL (/admin/<value>; /admin = the first). */
export const ADMIN_SECTIONS = [
  { value: 'rota', label: 'Generate rota', icon: IconWand },
  { value: 'people', label: 'Officers', icon: IconUsers },
  { value: 'locations', label: 'Clinics', icon: IconMapPin },
  { value: 'leave', label: 'Confirmed leave', icon: IconPlaneDeparture },
  { value: 'tally', label: 'Tally', icon: IconSum },
  { value: 'periods', label: 'Periods & holidays', icon: IconCalendarCog },
  { value: 'feedback', label: 'Feedback', icon: IconMessage2 },
  { value: 'reports', label: 'Reports', icon: IconFileText },
  { value: 'surat-arahan', label: 'Surat arahan', icon: IconFileCertificate },
] as const

export type AdminSection = (typeof ADMIN_SECTIONS)[number]['value']

/** The section a path shows: /admin/people -> 'people'; /admin or an unknown name -> 'rota'. */
export const adminSectionOf = (sub: string | undefined): AdminSection =>
  ADMIN_SECTIONS.find(s => s.value === sub)?.value ?? ADMIN_SECTIONS[0].value
