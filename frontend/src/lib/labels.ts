export const roleLabels: Record<string, string> = {
  Admin: 'Администратор',
  Manager: 'Руководитель',
}

export const userRoles = ['Manager', 'Admin'] as const

export type EvaluatorRole = 'Self' | 'Peer' | 'TeamLead' | 'Manager' | 'Rck' | 'ItLeader'
export type SessionStatus = 'Draft' | 'InProgress' | 'Overdue' | 'AwaitingDecision' | 'Closed' | 'Cancelled'
export type SessionType = 'Transition' | 'Confirmation'
export type ParticipantStatus = 'Pending' | 'InProgress' | 'Submitted' | 'Removed'

export const evaluatorRoleLabels: Record<EvaluatorRole, string> = {
  Self: 'Самооценка',
  Peer: 'Коллега',
  TeamLead: 'Лид',
  Manager: 'Менеджер',
  Rck: 'РЦК',
  ItLeader: 'ИТ-Лидер',
}

export const sessionStatusLabels: Record<SessionStatus, string> = {
  Draft: 'Черновик',
  InProgress: 'Идёт опрос',
  Overdue: 'Просрочена',
  AwaitingDecision: 'Ждёт решения',
  Closed: 'Закрыта',
  Cancelled: 'Отменена',
}

export const participantStatusLabels: Record<ParticipantStatus, string> = {
  Pending: 'Не открывал',
  InProgress: 'Заполняет',
  Submitted: 'Отправил',
  Removed: 'Удалён',
}

export const sessionTypeLabels: Record<SessionType, string> = {
  Transition: 'Переход на следующий грейд',
  Confirmation: 'Подтверждение текущего грейда',
}
