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

export type DecisionOutcome = 'Promoted' | 'GradeConfirmed' | 'NotConfirmed'

export const decisionOutcomeLabels: Record<DecisionOutcome, string> = {
  Promoted: 'Повышен',
  GradeConfirmed: 'Грейд подтверждён',
  NotConfirmed: 'Грейд не подтверждён',
}

export const auditActionLabels: Record<string, string> = {
  'session.launched': 'Опрос запущен',
  'session.cancelled': 'Сессия отменена',
  'session.closed_early': 'Опрос завершён досрочно',
  'session.draft_deleted': 'Черновик удалён',
  'session.decided': 'Принято решение',
  'participant.added': 'Добавлен респондент',
  'participant.removed': 'Удалён респондент',
  'participant.link_reissued': 'Перевыпущена ссылка',
}
