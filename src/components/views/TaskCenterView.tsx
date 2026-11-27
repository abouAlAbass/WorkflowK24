import React, { useState, useEffect, useCallback } from 'react';
import {
  Inbox,
  CheckCircle2,
  Clock,
  X,
  FileSpreadsheet,
  UserPlus,
  Hand,
  Unlock,
  RotateCcw,
  History,
  CalendarOff,
  Trash2,
  AlertCircle,
  KeyRound
} from 'lucide-react';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import { useSmartFormStore } from '../../store/useSmartFormStore';
import { getWorkflowRepository } from '../../services/repositoryManager';
import { SmartFormRenderer, evaluateFieldVisibility } from '../forms/SmartFormRenderer';
import { PersonalVaultPanel } from '../modals/PersonalVaultPanel';
import type {
  ActionItemTask,
  ApprovalOutcome,
  TaskAssignmentRecord,
  DelegationRuleRecord
} from '../../types/workflow';
import type { SmartFormDefinition, SmartFormVersionDetail } from '../../types/smartform';

export const TaskCenterView: React.FC = () => {
  const { actionItemTasks, resolveTask, users, syncDataFromRepository } = useWorkflowStore();
  const forms = useSmartFormStore((s) => s.forms);
  const fetchFormVersion = useSmartFormStore((s) => s.fetchFormVersion);

  const [selectedTask, setSelectedTask] = useState<ActionItemTask | null>(actionItemTasks[0] || null);
  const [selectedOutcome, setSelectedOutcome] = useState<ApprovalOutcome | null>(null);
  const [comment, setComment] = useState('');
  const [formPatch, setFormPatch] = useState<Record<string, unknown>>({});
  const [pinnedVersion, setPinnedVersion] = useState<SmartFormVersionDetail | null>(null);

  // Assignment history & action feedback
  const [assignments, setAssignments] = useState<TaskAssignmentRecord[]>([]);
  const [loadingAssignments, setLoadingAssignments] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);
  const [busyAction, setBusyAction] = useState(false);

  // Delegate modal state
  const [delegateModalOpen, setDelegateModalOpen] = useState(false);
  const [delegateTarget, setDelegateTarget] = useState('');
  const [delegateReason, setDelegateReason] = useState('');

  // Out-of-Office (Delegation Rules) modal state
  const [oooModalOpen, setOooModalOpen] = useState(false);
  const [delegationRules, setDelegationRules] = useState<DelegationRuleRecord[]>([]);
  const [oooDelegateId, setOooDelegateId] = useState('');
  const [oooScope, setOooScope] = useState('*');
  const [oooValidFrom, setOooValidFrom] = useState(() => new Date().toISOString().slice(0, 16));
  const [oooValidTo, setOooValidTo] = useState(() => {
    const nextWeek = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000);
    return nextWeek.toISOString().slice(0, 16);
  });
  const [oooReason, setOooReason] = useState('');
  const [oooError, setOooError] = useState<string | null>(null);

  // Personal Vault modal state (for user_sso_vault credential tasks)
  const [vaultModalOpen, setVaultModalOpen] = useState(false);
  const [vaultHighlightConnId, setVaultHighlightConnId] = useState<string | null>(null);

  const currentUserEmail = getWorkflowRepository().getAuthSession()?.user?.email || '';

  const parseRequiredConnectionId = (task: ActionItemTask | null): string | null => {
    if (!task) return null;
    if (task.metadataJson) {
      try {
        const parsed = JSON.parse(task.metadataJson);
        if (parsed && typeof parsed.requiredConnectionId === 'string') {
          return parsed.requiredConnectionId;
        }
      } catch {
        // ignore malformed metadata
      }
    }
    return null;
  };

  const selectedRequiredConnId = parseRequiredConnectionId(selectedTask);
  const isCredentialRequiredTask =
    Boolean(selectedRequiredConnId) ||
    Boolean(selectedTask?.title?.toLowerCase().includes('renseignez vos identifiants'));

  const loadAssignments = useCallback(async (taskId: string) => {
    setLoadingAssignments(true);
    try {
      const history = await getWorkflowRepository().getTaskAssignments(taskId);
      setAssignments(history);
    } catch {
      setAssignments([]);
    } finally {
      setLoadingAssignments(false);
    }
  }, []);

  const loadDelegationRules = useCallback(async () => {
    try {
      const rules = await getWorkflowRepository().getDelegationRules();
      setDelegationRules(rules);
    } catch {
      setDelegationRules([]);
    }
  }, []);

  useEffect(() => {
    setFormPatch({});
    setActionError(null);
    setActionSuccess(null);
    if (selectedTask?.id) {
      loadAssignments(selectedTask.id);
    } else {
      setAssignments([]);
    }
  }, [selectedTask?.id, loadAssignments]);

  useEffect(() => {
    let cancelled = false;
    if (selectedTask?.formId && selectedTask?.formVersionId) {
      fetchFormVersion(selectedTask.formId, selectedTask.formVersionId)
        .then((detail) => {
          if (!cancelled) {
            setPinnedVersion(detail);
          }
        })
        .catch(() => {
          if (!cancelled) {
            setPinnedVersion(null);
          }
        });
    } else {
      setPinnedVersion(null);
    }

    return () => {
      cancelled = true;
    };
  }, [selectedTask?.formId, selectedTask?.formVersionId, fetchFormVersion]);

  useEffect(() => {
    if (selectedTask) {
      const refreshed = actionItemTasks.find((t) => t.id === selectedTask.id);
      if (refreshed) {
        if (refreshed !== selectedTask) {
          setSelectedTask(refreshed);
        }
      } else {
        setSelectedTask(actionItemTasks[0] || null);
      }
    } else if (actionItemTasks.length > 0) {
      setSelectedTask(actionItemTasks[0]);
    }
  }, [actionItemTasks, selectedTask]);

  const handleSelectTask = (task: ActionItemTask) => {
    setSelectedTask(task);
    setSelectedOutcome(null);
    setComment('');
    setFormPatch({});
    setActionError(null);
    setActionSuccess(null);
  };

  const baseForm = selectedTask?.formId
    ? forms.find((f) => f.id === selectedTask.formId)
    : undefined;

  const boundForm: SmartFormDefinition | undefined = baseForm
    ? (pinnedVersion && pinnedVersion.smartFormId === baseForm.id
      ? {
        ...baseForm,
        fields: pinnedVersion.fields,
        latestVersionNumber: pinnedVersion.versionNumber,
        latestVersionStatus: pinnedVersion.status
      }
      : baseForm)
    : undefined;

  const handleFormValuesChange = (updatedValues: Record<string, unknown>) => {
    if (!selectedTask || selectedTask.formMode !== 'editable') {
      return;
    }

    const baseData = selectedTask.formData || {};
    const baseline: Record<string, unknown> = { ...baseData };
    if (boundForm) {
      for (const f of boundForm.fields) {
        if (f.type === 'section' || f.name in baseline) continue;
        if (f.type === 'checkbox') {
          baseline[f.name] = f.defaultValue === 'true';
        } else if (f.type === 'repeater') {
          baseline[f.name] = [];
        } else {
          baseline[f.name] = f.defaultValue ?? '';
        }
      }
    }

    const nextPatch: Record<string, unknown> = {};
    for (const [key, val] of Object.entries(updatedValues)) {
      if (boundForm) {
        const fieldDef = boundForm.fields.find((fld) => fld.name === key);
        if (!fieldDef || fieldDef.type === 'section' || fieldDef.readOnly) {
          continue;
        }
        if (!evaluateFieldVisibility(fieldDef.visibilityRule, updatedValues)) {
          continue;
        }
      }
      if (JSON.stringify(baseline[key]) !== JSON.stringify(val)) {
        nextPatch[key] = val;
      }
    }
    setFormPatch(nextPatch);
  };

  const handleConfirmDecision = () => {
    if (selectedTask && selectedOutcome) {
      const patchToSend =
        selectedTask.formMode === 'editable' && Object.keys(formPatch).length > 0
          ? formPatch
          : undefined;

      resolveTask(
        selectedTask.id,
        selectedOutcome.id,
        comment || `Outcome: ${selectedOutcome.label}`,
        patchToSend
      );

      setSelectedTask(null);
      setSelectedOutcome(null);
      setComment('');
      setFormPatch({});
    }
  };

  const handleDelegateSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedTask || !delegateTarget.trim()) return;
    setBusyAction(true);
    setActionError(null);
    try {
      await getWorkflowRepository().delegateTask(
        selectedTask.id,
        delegateTarget.trim(),
        delegateReason.trim() || undefined,
        selectedTask.version
      );
      setDelegateModalOpen(false);
      setDelegateTarget('');
      setDelegateReason('');
      setActionSuccess(`Task delegated to ${delegateTarget.trim()}.`);
      await syncDataFromRepository();
      await loadAssignments(selectedTask.id);
    } catch (err: unknown) {
      setActionError(err instanceof Error ? err.message : 'Failed to delegate task.');
    } finally {
      setBusyAction(false);
    }
  };

  const handleRecallTask = async () => {
    if (!selectedTask) return;
    setBusyAction(true);
    setActionError(null);
    try {
      await getWorkflowRepository().recallTask(selectedTask.id, 'Recalled from Task Inbox', selectedTask.version);
      setActionSuccess('Task recalled back to original assignee.');
      await syncDataFromRepository();
      await loadAssignments(selectedTask.id);
    } catch (err: unknown) {
      setActionError(err instanceof Error ? err.message : 'Failed to recall task.');
    } finally {
      setBusyAction(false);
    }
  };

  const handleClaimTask = async () => {
    if (!selectedTask) return;
    setBusyAction(true);
    setActionError(null);
    try {
      await getWorkflowRepository().claimTask(selectedTask.id, selectedTask.version);
      setActionSuccess('Task claimed exclusively.');
      await syncDataFromRepository();
      await loadAssignments(selectedTask.id);
    } catch (err: unknown) {
      setActionError(err instanceof Error ? err.message : 'Failed to claim task.');
    } finally {
      setBusyAction(false);
    }
  };

  const handleReleaseTask = async () => {
    if (!selectedTask) return;
    setBusyAction(true);
    setActionError(null);
    try {
      await getWorkflowRepository().releaseTask(selectedTask.id, 'Released back to group', selectedTask.version);
      setActionSuccess('Task claim released.');
      await syncDataFromRepository();
      await loadAssignments(selectedTask.id);
    } catch (err: unknown) {
      setActionError(err instanceof Error ? err.message : 'Failed to release task.');
    } finally {
      setBusyAction(false);
    }
  };

  const handleOpenOooModal = async () => {
    setOooError(null);
    setOooModalOpen(true);
    await loadDelegationRules();
  };

  const handleCreateDelegationRule = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!oooDelegateId.trim()) return;
    setOooError(null);
    setBusyAction(true);
    try {
      await getWorkflowRepository().createDelegationRule({
        delegateId: oooDelegateId.trim(),
        scope: oooScope.trim() || '*',
        validFrom: new Date(oooValidFrom).toISOString(),
        validTo: new Date(oooValidTo).toISOString(),
        reason: oooReason.trim() || undefined
      });
      setOooDelegateId('');
      setOooReason('');
      await loadDelegationRules();
    } catch (err: unknown) {
      setOooError(err instanceof Error ? err.message : 'Failed to create delegation rule.');
    } finally {
      setBusyAction(false);
    }
  };

  const handleDeleteDelegationRule = async (ruleId: string) => {
    setOooError(null);
    try {
      await getWorkflowRepository().deleteDelegationRule(ruleId);
      await loadDelegationRules();
    } catch (err: unknown) {
      setOooError(err instanceof Error ? err.message : 'Failed to delete delegation rule.');
    }
  };

  const taskOutcomes: ApprovalOutcome[] =
    selectedTask?.outcomes && selectedTask.outcomes.length > 0
      ? selectedTask.outcomes
      : [
        { id: 'approve', label: 'Approve Task', color: '#059669' },
        { id: 'reject', label: 'Reject Task', color: '#DC2626' }
      ];

  const isGroupOrClaimTask =
    selectedTask &&
    (selectedTask.assigneeType === 'Group' ||
      selectedTask.assigneeType === 'Role' ||
      selectedTask.resolutionPolicy === 'Claim' ||
      selectedTask.resolutionPolicy === 'Consensus' ||
      selectedTask.resolutionPolicy === 'Quorum');

  const isClaimedByCurrentUser =
    !!selectedTask?.claimedByUserId &&
    (!currentUserEmail ||
      selectedTask.claimedByUserId.toLowerCase() === currentUserEmail.toLowerCase());

  const isDelegatedTask =
    !!selectedTask?.onBehalfOfUserId || selectedTask?.status === ('DELEGATED' as ActionItemTask['status']);

  return (
    <div className="flex-1 overflow-y-auto p-6 space-y-6 select-none">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div className="text-start">
          <h2 className="text-xl font-extrabold text-[#0F172A] dark:text-white flex items-center gap-2">
            <Inbox className="w-5 h-5 text-[#2563EB] dark:text-blue-400" />
            Central Task Inbox & Approval Decisions
          </h2>
          <p className="text-xs text-[#64748B] dark:text-slate-400 font-medium">
            Review pending task worklists, manage delegations & group claims, and log binding decision outcomes
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => {
              setVaultHighlightConnId(null);
              setVaultModalOpen(true);
            }}
            className="inline-flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-bold bg-cyan-50 dark:bg-cyan-950/60 text-cyan-700 dark:text-cyan-300 border border-cyan-200 dark:border-cyan-800 hover:bg-cyan-100 dark:hover:bg-cyan-900/60 cursor-pointer transition-colors"
          >
            <KeyRound className="w-4 h-4" />
            Mes Identifiants Externes
          </button>

          <button
            type="button"
            onClick={handleOpenOooModal}
            className="inline-flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-bold bg-indigo-50 dark:bg-indigo-950/60 text-indigo-700 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-800 hover:bg-indigo-100 dark:hover:bg-indigo-900/60 cursor-pointer transition-colors"
          >
            <CalendarOff className="w-4 h-4" />
            My Delegations (Out-of-Office)
          </button>
        </div>
      </div>

      {actionError && (
        <div
          role="alert"
          className="p-3 rounded-2xl bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-800 text-xs text-red-700 dark:text-red-300 flex items-center gap-2"
        >
          <AlertCircle className="w-4 h-4 shrink-0" />
          <span>{actionError}</span>
        </div>
      )}

      {actionSuccess && (
        <div
          role="status"
          className="p-3 rounded-2xl bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800 text-xs text-emerald-700 dark:text-emerald-300 flex items-center gap-2"
        >
          <CheckCircle2 className="w-4 h-4 shrink-0" />
          <span>{actionSuccess}</span>
        </div>
      )}

      {actionItemTasks.length === 0 ? (
        <div className="p-12 text-center space-y-3 rounded-3xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs">
          <CheckCircle2 className="w-12 h-12 text-emerald-500 mx-auto" />
          <h3 className="text-base font-extrabold text-[#0F172A] dark:text-white">Task Inbox Clear!</h3>
          <p className="text-xs text-[#64748B] dark:text-slate-400 max-w-sm mx-auto">
            You have no pending approval decisions. All process instances are up to date.
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Left: Task List */}
          <div className="space-y-3">
            <span className="text-xs font-bold uppercase tracking-wider text-[#64748B] dark:text-slate-400 block px-1 text-start">
              Pending Inbox ({actionItemTasks.length})
            </span>

            {actionItemTasks.map((t) => {
              const isSelected = selectedTask?.id === t.id;
              return (
                <div
                  key={t.id}
                  onClick={() => handleSelectTask(t)}
                  className={`p-4 rounded-2xl border transition-all cursor-pointer text-start ${isSelected
                    ? 'border-[#2563EB] dark:border-blue-500 bg-blue-50/50 dark:bg-slate-800 shadow-sm'
                    : 'border-[#E2E8F0] dark:border-[#334155] bg-white dark:bg-[#1E293B] hover:border-slate-300 dark:hover:border-slate-700'
                    }`}
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-[10px] font-mono font-bold px-2 py-0.5 rounded bg-blue-50 dark:bg-blue-950 text-[#2563EB] dark:text-blue-400">
                      {t.id}
                    </span>
                    <span className="text-[10px] font-bold text-amber-600 dark:text-amber-400 flex items-center gap-1">
                      <Clock className="w-3 h-3" /> Due {t.dueDate}
                    </span>
                  </div>

                  <h4 className="text-xs font-extrabold text-[#0F172A] dark:text-white mt-2 leading-snug">
                    {t.title}
                  </h4>

                  <div className="flex flex-wrap items-center gap-1.5 mt-2">
                    {t.resolutionPolicy && t.resolutionPolicy !== 'Single' && (
                      <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-purple-50 dark:bg-purple-950/60 text-purple-700 dark:text-purple-300">
                        {t.resolutionPolicy}
                      </span>
                    )}
                    {t.claimedByUserId && (
                      <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300">
                        Claimed: {t.claimedByUserId}
                      </span>
                    )}
                    {t.onBehalfOfUserId && (
                      <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-amber-50 dark:bg-amber-950/60 text-amber-700 dark:text-amber-300">
                        On behalf of {t.onBehalfOfUserId}
                      </span>
                    )}
                  </div>

                  <div className="flex items-center gap-2 mt-2 pt-2 border-t border-slate-100 dark:border-slate-800 text-[11px] text-[#64748B] dark:text-slate-400">
                    <img src={t.requesterAvatar} alt={t.requester} className="w-5 h-5 rounded-full object-cover" />
                    <span>{t.requester} • {t.department}</span>
                  </div>
                </div>
              );
            })}
          </div>

          {/* Right: Selected Task Detail Inspector + Bound SmartForm + Assignment History */}
          {selectedTask && (
            <div className="lg:col-span-2 space-y-4">
              <div className="p-6 rounded-3xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs space-y-5 transition-colors text-start">
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-[#E2E8F0] dark:border-[#334155]">
                  <div>
                    <span className="text-[10px] font-mono font-bold text-[#2563EB] uppercase tracking-wider bg-blue-50 dark:bg-blue-950 px-2 py-0.5 rounded">
                      {selectedTask.workflowName} ({selectedTask.instanceId})
                    </span>
                    <h3 className="text-lg font-extrabold text-[#0F172A] dark:text-white mt-1">
                      {selectedTask.title}
                    </h3>
                  </div>

                  <div className="flex flex-wrap items-center gap-2">
                    {taskOutcomes.map((outcome) => (
                      <button
                        key={outcome.id}
                        type="button"
                        onClick={() => setSelectedOutcome(outcome)}
                        style={{ backgroundColor: outcome.color || '#2563EB' }}
                        className="px-4 py-2 hover:opacity-90 text-white rounded-xl text-xs font-bold shadow-md cursor-pointer transition-all"
                      >
                        {outcome.label}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Direct Personal Vault CTA for USER_CREDENTIAL_REQUIRED tasks */}
                {isCredentialRequiredTask && (
                  <div className="p-4 rounded-2xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                    <div className="flex items-start gap-2.5">
                      <KeyRound className="w-5 h-5 text-amber-600 dark:text-amber-400 shrink-0 mt-0.5" />
                      <div>
                        <p className="text-xs font-extrabold text-amber-900 dark:text-amber-200">
                          Identifiants personnels requis pour reprendre l&apos;exécution
                        </p>
                        <p className="text-[11px] text-amber-800 dark:text-amber-300">
                          Renseignez vos identifiants dans votre coffre personnel, puis complétez cette tâche pour relancer automatiquement l&apos;étape.
                        </p>
                      </div>
                    </div>
                    <button
                      type="button"
                      onClick={() => {
                        setVaultHighlightConnId(selectedRequiredConnId);
                        setVaultModalOpen(true);
                      }}
                      className="px-3.5 py-2 rounded-xl bg-amber-600 hover:bg-amber-700 text-white text-xs font-bold shrink-0 cursor-pointer"
                    >
                      Ouvrir Mes Identifiants Externes
                    </button>
                  </div>
                )}

                {/* Task Delegation & Group Slot Actions Bar */}
                <div className="flex flex-wrap items-center gap-2 p-3 rounded-2xl bg-[#F8FAFC] dark:bg-slate-900/60 border border-[#E2E8F0] dark:border-slate-800">
                  <button
                    type="button"
                    disabled={busyAction}
                    onClick={() => setDelegateModalOpen(true)}
                    className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-xs font-bold bg-white dark:bg-slate-800 border border-[#E2E8F0] dark:border-slate-700 text-[#0F172A] dark:text-slate-100 hover:border-[#2563EB] cursor-pointer transition-colors disabled:opacity-50"
                  >
                    <UserPlus className="w-3.5 h-3.5 text-[#2563EB]" />
                    Delegate
                  </button>

                  {isGroupOrClaimTask && !selectedTask.claimedByUserId && (
                    <button
                      type="button"
                      disabled={busyAction}
                      onClick={handleClaimTask}
                      className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-xs font-bold bg-emerald-600 hover:bg-emerald-700 text-white cursor-pointer transition-colors disabled:opacity-50"
                    >
                      <Hand className="w-3.5 h-3.5" />
                      Claim Task
                    </button>
                  )}

                  {selectedTask.claimedByUserId && isClaimedByCurrentUser && (
                    <button
                      type="button"
                      disabled={busyAction}
                      onClick={handleReleaseTask}
                      className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-xs font-bold bg-amber-500 hover:bg-amber-600 text-white cursor-pointer transition-colors disabled:opacity-50"
                    >
                      <Unlock className="w-3.5 h-3.5" />
                      Release Claim
                    </button>
                  )}

                  {isDelegatedTask && (
                    <button
                      type="button"
                      disabled={busyAction}
                      onClick={handleRecallTask}
                      className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-xs font-bold bg-slate-200 dark:bg-slate-700 text-[#0F172A] dark:text-slate-100 hover:bg-slate-300 dark:hover:bg-slate-600 cursor-pointer transition-colors disabled:opacity-50"
                    >
                      <RotateCcw className="w-3.5 h-3.5" />
                      Recall
                    </button>
                  )}

                  {selectedTask.claimedByUserId && (
                    <span className="ms-auto text-[11px] font-semibold text-emerald-700 dark:text-emerald-400">
                      Locked by {selectedTask.claimedByUserId}
                    </span>
                  )}
                </div>

                <div className="grid grid-cols-2 gap-4 text-xs">
                  <div className="p-3 rounded-2xl bg-[#F8FAFC] dark:bg-slate-900/60 border border-[#E2E8F0] dark:border-slate-800">
                    <span className="text-[10px] font-bold text-[#64748B] dark:text-slate-400 block uppercase">Requester</span>
                    <div className="flex items-center gap-2 mt-1">
                      <img src={selectedTask.requesterAvatar} alt={selectedTask.requester} className="w-6 h-6 rounded-full" />
                      <span className="font-extrabold text-[#0F172A] dark:text-slate-100">{selectedTask.requester} ({selectedTask.department})</span>
                    </div>
                  </div>

                  <div className="p-3 rounded-2xl bg-[#F8FAFC] dark:bg-slate-900/60 border border-[#E2E8F0] dark:border-slate-800">
                    <span className="text-[10px] font-bold text-[#64748B] dark:text-slate-400 block uppercase">Assignment & Policy</span>
                    <span className="font-extrabold text-[#0F172A] dark:text-slate-100 mt-1 block">
                      {selectedTask.assigneeId || selectedTask.department} • {selectedTask.resolutionPolicy || 'Single'}
                    </span>
                  </div>
                </div>

                <div className="space-y-2">
                  <h4 className="text-xs font-bold text-[#0F172A] dark:text-slate-200">Requisition Description & Notes</h4>
                  <div className="p-4 rounded-2xl bg-[#F8FAFC] dark:bg-slate-900/60 border border-[#E2E8F0] dark:border-slate-800 text-xs text-[#0F172A] dark:text-slate-200 font-medium leading-relaxed">
                    {selectedTask.description}
                  </div>
                </div>

                {boundForm && (
                  <div className="pt-3 border-t border-slate-200 dark:border-slate-800 space-y-3">
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-extrabold text-[#0F172A] dark:text-white flex items-center gap-1.5">
                        <FileSpreadsheet className="w-4 h-4 text-[#2563EB] dark:text-blue-400" />
                        <span>Bound SmartForm: {boundForm.title}</span>
                        {pinnedVersion && (
                          <span
                            data-testid="task-pinned-form-version"
                            className="text-[10px] font-mono px-2 py-0.5 rounded-full bg-blue-500/10 text-[#2563EB] dark:text-blue-400"
                          >
                            {`v${pinnedVersion.versionNumber} • ${pinnedVersion.status}`}
                          </span>
                        )}
                      </span>
                      <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
                        {selectedTask.formMode === 'editable' ? 'Editable Mode' : 'Read-Only Review'}
                      </span>
                    </div>

                    <div className="p-4 rounded-2xl border border-slate-200 dark:border-slate-800 bg-[#F8FAFC] dark:bg-slate-900/50">
                      <SmartFormRenderer
                        key={selectedTask.id}
                        form={boundForm}
                        initialValues={selectedTask.formData}
                        forceReadOnly={selectedTask.formMode !== 'editable'}
                        hideSubmitButton={true}
                        onChangeValues={selectedTask.formMode === 'editable' ? handleFormValuesChange : undefined}
                      />
                    </div>
                  </div>
                )}

                {/* Append-Only Assignment History */}
                <div className="pt-3 border-t border-slate-200 dark:border-slate-800 space-y-2">
                  <h4 className="text-xs font-extrabold text-[#0F172A] dark:text-white flex items-center gap-1.5">
                    <History className="w-4 h-4 text-[#2563EB] dark:text-blue-400" />
                    Assignment History
                  </h4>
                  {loadingAssignments ? (
                    <p className="text-xs text-[#64748B] dark:text-slate-400">Loading assignment history...</p>
                  ) : assignments.length === 0 ? (
                    <p className="text-xs text-[#64748B] dark:text-slate-400">No assignment transitions recorded yet.</p>
                  ) : (
                    <ul className="space-y-2">
                      {assignments.map((a) => (
                        <li
                          key={a.id}
                          className="p-3 rounded-xl bg-[#F8FAFC] dark:bg-slate-900/50 border border-[#E2E8F0] dark:border-slate-800 text-xs flex flex-col sm:flex-row sm:items-center justify-between gap-2"
                        >
                          <div className="space-y-0.5">
                            <span className="inline-block text-[10px] font-mono font-bold uppercase px-2 py-0.5 rounded bg-blue-50 dark:bg-blue-950 text-[#2563EB] dark:text-blue-400 me-2">
                              {a.action}
                            </span>
                            <span className="font-semibold text-[#0F172A] dark:text-slate-100">
                              {a.fromAssigneeId ? `${a.fromAssigneeId} → ` : ''}{a.toAssigneeId}
                            </span>
                            {a.reason && (
                              <p className="text-[11px] text-[#64748B] dark:text-slate-400">{a.reason}</p>
                            )}
                          </div>
                          <span className="text-[10px] text-[#64748B] dark:text-slate-400 shrink-0">
                            by {a.actorUserId} • {new Date(a.createdAt).toLocaleString()}
                          </span>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Decision Modal */}
      {selectedTask && selectedOutcome && (
        <div className="fixed inset-0 z-50 bg-slate-950/60 backdrop-blur-xs flex items-center justify-center p-4 select-none animate-fade-in text-[#0F172A] dark:text-slate-100">
          <div className="border border-[#E2E8F0] dark:border-[#334155] bg-white dark:bg-[#1E293B] rounded-3xl w-full max-w-lg shadow-2xl overflow-hidden flex flex-col transition-colors">
            <div className="p-4 border-b border-[#E2E8F0] dark:border-[#334155] bg-[#F8FAFC] dark:bg-[#0F172A] flex items-center justify-between">
              <h3 className="font-extrabold text-sm text-[#0F172A] dark:text-white">
                Log Task Decision: {selectedOutcome.label}
              </h3>
              <button type="button" onClick={() => setSelectedOutcome(null)} className="p-1 text-slate-400 hover:text-slate-600">
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-5 space-y-4 text-start">
              <p className="text-xs text-[#64748B] dark:text-slate-300">
                Confirm decision <strong>"{selectedOutcome.label}"</strong> for <strong>"{selectedTask.title}"</strong>?
              </p>
              <label htmlFor="task-decision-comment" className="block text-xs font-bold text-[#0F172A] dark:text-slate-200">
                Justification Comment
              </label>
              <textarea
                id="task-decision-comment"
                value={comment}
                onChange={(e) => setComment(e.target.value)}
                placeholder="Enter justification comment..."
                rows={3}
                className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-3 text-xs text-[#0F172A] dark:text-slate-100 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-[#2563EB]"
              />
            </div>

            <div className="p-4 border-t border-[#E2E8F0] dark:border-[#334155] bg-[#F8FAFC] dark:bg-[#0F172A] flex justify-end gap-2">
              <button type="button" onClick={() => setSelectedOutcome(null)} className="px-4 py-2 rounded-xl bg-slate-200 dark:bg-slate-800 text-xs font-bold cursor-pointer">
                Cancel
              </button>
              <button
                type="button"
                onClick={handleConfirmDecision}
                style={{ backgroundColor: selectedOutcome.color || '#2563EB' }}
                className="px-4 py-2 rounded-xl text-xs font-bold text-white cursor-pointer"
              >
                Confirm {selectedOutcome.label}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Delegate Task Modal */}
      {selectedTask && delegateModalOpen && (
        <div className="fixed inset-0 z-50 bg-slate-950/60 backdrop-blur-xs flex items-center justify-center p-4 select-none text-[#0F172A] dark:text-slate-100">
          <form
            onSubmit={handleDelegateSubmit}
            className="border border-[#E2E8F0] dark:border-[#334155] bg-white dark:bg-[#1E293B] rounded-3xl w-full max-w-lg shadow-2xl overflow-hidden flex flex-col"
          >
            <div className="p-4 border-b border-[#E2E8F0] dark:border-[#334155] bg-[#F8FAFC] dark:bg-[#0F172A] flex items-center justify-between">
              <h3 className="font-extrabold text-sm text-[#0F172A] dark:text-white flex items-center gap-2">
                <UserPlus className="w-4 h-4 text-[#2563EB]" />
                Delegate Task
              </h3>
              <button type="button" onClick={() => setDelegateModalOpen(false)} className="p-1 text-slate-400 hover:text-slate-600">
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-5 space-y-4 text-start">
              <div>
                <label htmlFor="delegate-target-user" className="block text-xs font-bold mb-1">
                  Delegate To (User Email)
                </label>
                <input
                  id="delegate-target-user"
                  list="delegate-users-list"
                  type="text"
                  required
                  value={delegateTarget}
                  onChange={(e) => setDelegateTarget(e.target.value)}
                  placeholder="e.g. karim.khmissi@corp.internal"
                  className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2.5 text-xs focus:outline-none focus:ring-2 focus:ring-[#2563EB]"
                />
                <datalist id="delegate-users-list">
                  {users.map((u) => (
                    <option key={u.id} value={u.email}>
                      {u.name} ({u.role})
                    </option>
                  ))}
                </datalist>
              </div>

              <div>
                <label htmlFor="delegate-reason" className="block text-xs font-bold mb-1">
                  Reason (optional)
                </label>
                <textarea
                  id="delegate-reason"
                  rows={2}
                  value={delegateReason}
                  onChange={(e) => setDelegateReason(e.target.value)}
                  placeholder="Out of office / workload balancing..."
                  className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2.5 text-xs focus:outline-none focus:ring-2 focus:ring-[#2563EB]"
                />
              </div>
            </div>

            <div className="p-4 border-t border-[#E2E8F0] dark:border-[#334155] bg-[#F8FAFC] dark:bg-[#0F172A] flex justify-end gap-2">
              <button type="button" onClick={() => setDelegateModalOpen(false)} className="px-4 py-2 rounded-xl bg-slate-200 dark:bg-slate-800 text-xs font-bold cursor-pointer">
                Cancel
              </button>
              <button
                type="submit"
                disabled={busyAction}
                className="px-4 py-2 rounded-xl bg-[#2563EB] hover:bg-blue-700 text-white text-xs font-bold cursor-pointer disabled:opacity-50"
              >
                Confirm Delegation
              </button>
            </div>
          </form>
        </div>
      )}

      {/* My Delegations (Out-of-Office) Modal */}
      {oooModalOpen && (
        <div className="fixed inset-0 z-50 bg-slate-950/60 backdrop-blur-xs flex items-center justify-center p-4 select-none text-[#0F172A] dark:text-slate-100">
          <div className="border border-[#E2E8F0] dark:border-[#334155] bg-white dark:bg-[#1E293B] rounded-3xl w-full max-w-2xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh]">
            <div className="p-4 border-b border-[#E2E8F0] dark:border-[#334155] bg-[#F8FAFC] dark:bg-[#0F172A] flex items-center justify-between">
              <h3 className="font-extrabold text-sm text-[#0F172A] dark:text-white flex items-center gap-2">
                <CalendarOff className="w-4 h-4 text-indigo-600 dark:text-indigo-400" />
                My Delegations (Out-of-Office Rules)
              </h3>
              <button type="button" onClick={() => setOooModalOpen(false)} className="p-1 text-slate-400 hover:text-slate-600">
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-5 space-y-5 overflow-y-auto text-start">
              {oooError && (
                <div
                  role="alert"
                  className="p-3 rounded-xl bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-800 text-xs text-red-700 dark:text-red-300 flex items-center gap-2"
                >
                  <AlertCircle className="w-4 h-4 shrink-0" />
                  <span>{oooError}</span>
                </div>
              )}

              <form onSubmit={handleCreateDelegationRule} className="p-4 rounded-2xl bg-[#F8FAFC] dark:bg-slate-900/60 border border-[#E2E8F0] dark:border-slate-800 space-y-3">
                <h4 className="text-xs font-extrabold">Add Temporary Delegation Rule</h4>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div>
                    <label htmlFor="ooo-delegate-id" className="block text-[11px] font-bold mb-1">
                      Delegate Email
                    </label>
                    <input
                      id="ooo-delegate-id"
                      list="ooo-users-list"
                      type="text"
                      required
                      value={oooDelegateId}
                      onChange={(e) => setOooDelegateId(e.target.value)}
                      placeholder="delegate@corp.internal"
                      className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs"
                    />
                    <datalist id="ooo-users-list">
                      {users.map((u) => (
                        <option key={u.id} value={u.email}>
                          {u.name}
                        </option>
                      ))}
                    </datalist>
                  </div>

                  <div>
                    <label htmlFor="ooo-scope" className="block text-[11px] font-bold mb-1">
                      Workflow Scope (* for all)
                    </label>
                    <input
                      id="ooo-scope"
                      type="text"
                      value={oooScope}
                      onChange={(e) => setOooScope(e.target.value)}
                      placeholder="*"
                      className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs"
                    />
                  </div>

                  <div>
                    <label htmlFor="ooo-valid-from" className="block text-[11px] font-bold mb-1">
                      Valid From
                    </label>
                    <input
                      id="ooo-valid-from"
                      type="datetime-local"
                      required
                      value={oooValidFrom}
                      onChange={(e) => setOooValidFrom(e.target.value)}
                      className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs"
                    />
                  </div>

                  <div>
                    <label htmlFor="ooo-valid-to" className="block text-[11px] font-bold mb-1">
                      Valid To
                    </label>
                    <input
                      id="ooo-valid-to"
                      type="datetime-local"
                      required
                      value={oooValidTo}
                      onChange={(e) => setOooValidTo(e.target.value)}
                      className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs"
                    />
                  </div>
                </div>

                <div>
                  <label htmlFor="ooo-reason" className="block text-[11px] font-bold mb-1">
                    Reason (optional)
                  </label>
                  <input
                    id="ooo-reason"
                    type="text"
                    value={oooReason}
                    onChange={(e) => setOooReason(e.target.value)}
                    placeholder="Annual leave / travel"
                    className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs"
                  />
                </div>

                <div className="flex justify-end">
                  <button
                    type="submit"
                    disabled={busyAction}
                    className="px-4 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold cursor-pointer disabled:opacity-50"
                  >
                    Create Rule
                  </button>
                </div>
              </form>

              <div className="space-y-2">
                <h4 className="text-xs font-extrabold">Active Rules ({delegationRules.length})</h4>
                {delegationRules.length === 0 ? (
                  <p className="text-xs text-[#64748B] dark:text-slate-400">No Out-of-Office delegation rules configured.</p>
                ) : (
                  <ul className="space-y-2">
                    {delegationRules.map((rule) => (
                      <li
                        key={rule.id}
                        className="p-3 rounded-xl bg-[#F8FAFC] dark:bg-slate-900/50 border border-[#E2E8F0] dark:border-slate-800 flex items-center justify-between gap-3 text-xs"
                      >
                        <div>
                          <div className="font-bold">
                            {rule.delegatorId} → {rule.delegateId}
                            <span className="ms-2 text-[10px] font-mono px-1.5 py-0.5 rounded bg-indigo-50 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300">
                              scope: {rule.scope}
                            </span>
                          </div>
                          <div className="text-[11px] text-[#64748B] dark:text-slate-400">
                            {new Date(rule.validFrom).toLocaleString()} → {new Date(rule.validTo).toLocaleString()}
                          </div>
                        </div>
                        <button
                          type="button"
                          onClick={() => handleDeleteDelegationRule(rule.id)}
                          aria-label={`Delete delegation rule to ${rule.delegateId}`}
                          className="p-1.5 rounded-lg text-red-600 hover:bg-red-50 dark:hover:bg-red-950/40 cursor-pointer"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Personal Vault Modal (Mes Identifiants Externes) */}
      {vaultModalOpen && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Mes Identifiants Externes"
          className="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4"
        >
          <div className="bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] rounded-3xl shadow-2xl max-w-2xl w-full max-h-[90vh] overflow-y-auto p-4 space-y-3">
            <div className="flex items-center justify-between px-2">
              <span className="text-xs font-extrabold text-[#0F172A] dark:text-white">
                Coffre personnel
              </span>
              <button
                type="button"
                onClick={() => setVaultModalOpen(false)}
                aria-label="Fermer le coffre personnel"
                className="p-1.5 rounded-xl hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
            <PersonalVaultPanel
              highlightConnectionId={vaultHighlightConnId}
              onCredentialSaved={() => {
                setActionSuccess('Identifiants enregistrés. Vous pouvez maintenant compléter la tâche pour reprendre l\'exécution.');
              }}
            />
          </div>
        </div>
      )}
    </div>
  );
};
