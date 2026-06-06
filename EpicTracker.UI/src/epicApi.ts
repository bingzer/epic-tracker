import { api } from './client';
import type { Epic, AuditLog, Spec } from './types';

export interface CreateEpicPayload {
  epicAgentName: string;
  brief: string;
  name?: string;
  codingAgentNames?: string[];
  needsMockup: boolean;
  reviewerAgentName?: string;
  isACRequired: boolean;
  isCodeReviewRequired: boolean;
}

export const EpicApi = {
  list: () => api.get<Epic[]>('/api/epics'),

  get: (epicId: string) => api.get<Epic>(`/api/epics/${epicId}`),

  create: (payload: CreateEpicPayload) => api.post<Epic>('/api/epics', payload),

  update: (epicId: string, payload: Partial<Epic>) => api.put<Epic>(`/api/epics/${epicId}`, payload),

  getHistory: (epicId: string) => api.get<AuditLog[]>(`/api/epics/${epicId}/history`),

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

  delete: (epicId: string) => api.delete(`/api/epics/${epicId}`),

  forceState: (epicId: string, stateName: string) =>
    api.post<Epic>(`/api/epics/${epicId}/force-state`, { stateName }),

  createSpec: (epicId: string, assignedAgentName: string, specDocPath: string | null, codeReviewRequired: boolean, reviewerAgentName: string | null) =>
    api.post<Spec>(`/api/epics/${epicId}/specs`, { assignedAgentName, specDocPath, codeReviewRequired, reviewerAgentName }),
};

export interface AgentStatus {
  sessionName: string;
  lastStatus: 'running' | 'idle' | 'offline';
  lastSeen: string | null;
}

export const AgentApi = {
  list: () => api.get<AgentStatus[]>('/api/agents'),
};

export const TemplateApi = {
  getGovernance: () => api.getText('/api/templates/governance'),
  saveGovernance: (content: string) => api.put<void>('/api/templates/governance', { content }),
};

export const DocApi = {
  get: (path: string) => api.getText(`/api/docs?path=${encodeURIComponent(path)}`),
  save: (path: string, content: string) => api.put<void>('/api/docs', { path, content }),
};

export const SpecApi = {
  get: (specId: string) => api.get<Spec>(`/api/specs/${specId}`),

  update: (specId: string, payload: Partial<Spec>) => api.put<Spec>(`/api/specs/${specId}`, payload),

  advance: (specId: string) => api.post<Spec>(`/api/specs/${specId}/advance`),

  approveHumanInLoop: (specId: string, isApproved: boolean, humanInput: string | null) =>
    api.post<void>(`/api/specs/${specId}/approve-human-in-loop`, { isApproved, humanInput }),

  forceState: (specId: string, stateName: string) =>
    api.post<Spec>(`/api/specs/${specId}/force-state`, { stateName }),

  codeNow: (specId: string) => api.post<Spec>(`/api/specs/${specId}/ready`),
};
