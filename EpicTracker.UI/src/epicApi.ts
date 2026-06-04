import { api } from './client';
import type { Epic, EpicAudit, Spec } from './types';

export interface CreateEpicPayload {
  epicAgent: string;
  brief: string;
  name?: string;
  codingAgents?: string[];
  needsMockup: boolean;
  reviewerAgentId?: string;
}

export const EpicApi = {
  list: () => api.get<Epic[]>('/api/epics'),

  get: (epicId: string) => api.get<Epic>(`/api/epics/${epicId}`),

  create: (payload: CreateEpicPayload) => api.post<Epic>('/api/epics', payload),

  update: (epicId: string, payload: Partial<Epic>) => api.put<Epic>(`/api/epics/${epicId}`, payload),

  getHistory: (epicId: string) => api.get<EpicAudit[]>(`/api/epics/${epicId}/history`),

  advance: (epicId: string, epicAgentId: string) =>
    api.post<Epic>(`/api/epics/${epicId}/advance`, { epicAgentId }),

  approveHumanInLoop: (epicId: string, isApproved: boolean, humanInput: string | null) =>
    api.post<Epic>(`/api/epics/${epicId}/approve-human-in-loop`, { isApproved, humanInput }),

  raiseHumanInLoop: (epicId: string, questions: string, approveToStateName: string, rejectToStateName: string) =>
    api.post<void>(`/api/epics/${epicId}/raise-human-in-loop`, { questions, approveToStateName, rejectToStateName }),

  raiseAgentSwarm: (epicId: string, objective: string, toStateName: string) =>
    api.post<void>(`/api/epics/${epicId}/raise-agent-swarm`, { objective, toStateName }),

  submitAgreement: (epicId: string, agentId: string, hasAgreed: boolean, note: string | null) =>
    api.post<void>(`/api/epics/${epicId}/submit-agreement`, { agentId, hasAgreed, note }),

  wakeAgent: (epicId: string) => api.post<void>(`/api/epics/${epicId}/wake-agent`),

  createSpec: (epicId: string, assignedAgentId: string, specDocPath: string | null, codeReviewRequired: boolean, reviewerAgentId: string | null) =>
    api.post<Spec>(`/api/epics/${epicId}/specs`, { assignedAgentId, specDocPath, codeReviewRequired, reviewerAgentId }),
};

export const SpecApi = {
  get: (specId: string) => api.get<Spec>(`/api/specs/${specId}`),

  update: (specId: string, payload: Partial<Spec>) => api.put<Spec>(`/api/specs/${specId}`, payload),

  advance: (specId: string) => api.post<Spec>(`/api/specs/${specId}/advance`),

  approveHumanInLoop: (specId: string, isApproved: boolean, humanInput: string | null) =>
    api.post<void>(`/api/specs/${specId}/approve-human-in-loop`, { isApproved, humanInput }),
};
