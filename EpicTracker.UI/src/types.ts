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
  assignedAgentId: string;
  reviewerAgentName: string | null;
  codeReviewRequired: boolean;
  specDocPath: string | null;
  isSpecApproved: boolean;
  isAbandoned: boolean;
  isSpecDrafted: boolean;
  isAcPassed: boolean | null;
  isCodeDone: boolean;
  isCodeReviewApproved: boolean | null;
  currentStateName: string;
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
  createdAt: string;
  specs: Spec[];
  currentStateName: string;
  epicAgentInstruction: string | null;
  humanInLoop: HumanInLoop | null;
  agentSwarm: AgentSwarm | null;
}

export interface EpicAudit {
  id: number;
  epicId: string;
  epicAgentId: string;
  fromState: string;
  toState: string;
  epicAgentInstruction: string | null;
  timestamp: string;
}
