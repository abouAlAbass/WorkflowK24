import type { WorkflowRepository, AppMode } from './workflowRepository';
import { InMemoryWorkflowRepository } from './inMemoryWorkflowRepository';
import { HttpWorkflowRepository } from './httpWorkflowRepository';

const MODE_STORAGE_KEY = 'workflow_engine_active_mode';

class RepositoryManager {
  private currentMode: AppMode = 'demo';
  private inMemoryRepo: InMemoryWorkflowRepository;
  private httpRepo: HttpWorkflowRepository;

  constructor() {
    this.inMemoryRepo = new InMemoryWorkflowRepository();
    this.httpRepo = new HttpWorkflowRepository();

    const savedMode = localStorage.getItem(MODE_STORAGE_KEY) as AppMode | null;
    if (savedMode === 'connected' || savedMode === 'demo') {
      this.currentMode = savedMode;
    }
  }

  getMode(): AppMode {
    return this.currentMode;
  }

  setMode(mode: AppMode): void {
    this.currentMode = mode;
    localStorage.setItem(MODE_STORAGE_KEY, mode);
  }

  getRepository(): WorkflowRepository {
    return this.currentMode === 'connected' ? this.httpRepo : this.inMemoryRepo;
  }

  getHttpRepository(): WorkflowRepository {
    return this.httpRepo;
  }

  isConfigured(): boolean {
    return localStorage.getItem(MODE_STORAGE_KEY) !== null;
  }
}

export const repositoryManager = new RepositoryManager();
export const getWorkflowRepository = (): WorkflowRepository => repositoryManager.getRepository();
