export interface AgentAgreement {
  agentId: string;
  hasAgreed: boolean | null;
  note: string | null;
}

export interface AgentSwarm {
  objective: string;
  toStateName: string;
  iteration: number;
  agreements: AgentAgreement[];
  humanInput: string | null;
  hasConsensus: boolean;
  hasDisagreement: boolean;
  isComplete: boolean;
}

export interface HumanInLoop {
  questions: string;
  humanInput: string | null;
  isApproved: boolean | null;
  approveToStateName: string;
  rejectToStateName: string;
}

export interface Spec {
  id: string;
  epicId: string;
  assignedAgentName: string;
  reviewerAgentName: string | null;
  isCodeReviewRequired: boolean | null;
  isACRequired: boolean | null;
  specDocPath: string | null;
  isSpecApproved: boolean;
  isAbandoned: boolean;
  isSpecDrafted: boolean;
  isAcPassed: boolean | null;
  isCodeDone: boolean;
  isCodeReviewApproved: boolean | null;
  currentStateName: string;
  lastKnownStateName: string | null;
  dependsOn: string[];
  epicAgentInstruction: string | null;
  humanInLoop: HumanInLoop | null;
  agentSwarm: AgentSwarm | null;
}

export interface Epic {
  id: string;
  name: string | null;
  epicAgentName: string;
  brief: string | null;
  slug: string;
  epicDocumentPath: string;
  epicGovernancePath: string;
  codingAgentNames: string[];
  needsMockup: boolean;
  isDocDrafted: boolean;
  mockupPath: string | null;
  isMockupDone: boolean;
  isSpecListApproved: boolean;
  reviewerAgentName: string | null;
  isACRequired: boolean;
  isCodeReviewRequired: boolean;
  createdAt: string;
  specs: Spec[];
  currentStateName: string;
  epicAgentInstruction: string | null;
  humanInLoop: HumanInLoop | null;
  agentSwarm: AgentSwarm | null;
  lastKnownStateName: string | null;
}

export interface AuditLog {
  id: number;
  timestamp: string;
  action: string;
  epicState: string;
  specState: string | null;
  epicId: string;
  specId: string | null;
  actor: string | null;
  message: string | null;
}
