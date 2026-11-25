import React, { useState, useEffect } from 'react';
import { Inbox, CheckCircle2, Clock, X, FileSpreadsheet } from 'lucide-react';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import { useSmartFormStore } from '../../store/useSmartFormStore';
import { SmartFormRenderer, evaluateFieldVisibility } from '../forms/SmartFormRenderer';
import type { ActionItemTask, ApprovalOutcome } from '../../types/workflow';

export const TaskCenterView: React.FC = () => {
  const { actionItemTasks, resolveTask } = useWorkflowStore();
  const forms = useSmartFormStore((s) => s.forms);

  const [selectedTask, setSelectedTask] = useState<ActionItemTask | null>(actionItemTasks[0] || null);
  const [selectedOutcome, setSelectedOutcome] = useState<ApprovalOutcome | null>(null);
  const [comment, setComment] = useState('');
  const [formPatch, setFormPatch] = useState<Record<string, unknown>>({});

  useEffect(() => {
    setFormPatch({});
  }, [selectedTask?.id]);

  useEffect(() => {
    if (selectedTask && !actionItemTasks.some((t) => t.id === selectedTask.id)) {
      setSelectedTask(actionItemTasks[0] || null);
    } else if (!selectedTask && actionItemTasks.length > 0) {
      setSelectedTask(actionItemTasks[0]);
    }
  }, [actionItemTasks, selectedTask]);

  const handleSelectTask = (task: ActionItemTask) => {
    setSelectedTask(task);
    setSelectedOutcome(null);
    setComment('');
    setFormPatch({});
  };

  const boundForm = selectedTask?.formId
    ? forms.find((f) => f.id === selectedTask.formId)
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

  const taskOutcomes: ApprovalOutcome[] =
    selectedTask?.outcomes && selectedTask.outcomes.length > 0
      ? selectedTask.outcomes
      : [
          { id: 'approve', label: 'Approve Task', color: '#059669' },
          { id: 'reject', label: 'Reject Task', color: '#DC2626' }
        ];

  return (
    <div className="flex-1 overflow-y-auto p-6 space-y-6 select-none">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-extrabold text-[#0F172A] dark:text-white flex items-center gap-2">
            <Inbox className="w-5 h-5 text-[#2563EB] dark:text-blue-400" />
            Central Task Inbox & Approval Decisions
          </h2>
          <p className="text-xs text-[#64748B] dark:text-slate-400 font-medium">
            Review pending task worklists, inspect bound SmartForms, and log binding decision outcomes
          </p>
        </div>
      </div>

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
            <span className="text-xs font-bold uppercase tracking-wider text-[#64748B] dark:text-slate-400 block px-1">
              Pending Inbox ({actionItemTasks.length})
            </span>

            {actionItemTasks.map((t) => {
              const isSelected = selectedTask?.id === t.id;
              return (
                <div
                  key={t.id}
                  onClick={() => handleSelectTask(t)}
                  className={`p-4 rounded-2xl border transition-all cursor-pointer ${
                    isSelected
                      ? 'border-[#2563EB] dark:border-blue-500 bg-blue-50/50 dark:bg-slate-800 shadow-sm'
                      : 'border-[#E2E8F0] dark:border-[#334155] bg-white dark:bg-[#1E293B] hover:border-slate-300 dark:hover:border-slate-700'
                  }`}
                >
                  <div className="flex items-center justify-between">
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

                  <div className="flex items-center gap-2 mt-2 pt-2 border-t border-slate-100 dark:border-slate-800 text-[11px] text-[#64748B] dark:text-slate-400">
                    <img src={t.requesterAvatar} alt={t.requester} className="w-5 h-5 rounded-full object-cover" />
                    <span>{t.requester} • {t.department}</span>
                  </div>
                </div>
              );
            })}
          </div>

          {/* Right: Selected Task Detail Inspector + Bound SmartForm */}
          {selectedTask && (
            <div className="lg:col-span-2 space-y-4">
              <div className="p-6 rounded-3xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs space-y-5 transition-colors">
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

                <div className="grid grid-cols-2 gap-4 text-xs">
                  <div className="p-3 rounded-2xl bg-[#F8FAFC] dark:bg-slate-900/60 border border-[#E2E8F0] dark:border-slate-800">
                    <span className="text-[10px] font-bold text-[#64748B] dark:text-slate-400 block uppercase">Requester</span>
                    <div className="flex items-center gap-2 mt-1">
                      <img src={selectedTask.requesterAvatar} alt={selectedTask.requester} className="w-6 h-6 rounded-full" />
                      <span className="font-extrabold text-[#0F172A] dark:text-slate-100">{selectedTask.requester} ({selectedTask.department})</span>
                    </div>
                  </div>

                  <div className="p-3 rounded-2xl bg-[#F8FAFC] dark:bg-slate-900/60 border border-[#E2E8F0] dark:border-slate-800">
                    <span className="text-[10px] font-bold text-[#64748B] dark:text-slate-400 block uppercase">Priority & Amount</span>
                    <span className="font-extrabold text-[#0F172A] dark:text-slate-100 mt-1 block">
                      {selectedTask.priority} Priority • {selectedTask.amount || 'N/A'}
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
              <button onClick={() => setSelectedOutcome(null)} className="p-1 text-slate-400 hover:text-slate-600">
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-5 space-y-4">
              <p className="text-xs text-[#64748B] dark:text-slate-300">
                Confirm decision <strong>"{selectedOutcome.label}"</strong> for <strong>"{selectedTask.title}"</strong>?
              </p>
              <textarea
                value={comment}
                onChange={(e) => setComment(e.target.value)}
                placeholder="Enter justification comment..."
                rows={3}
                className="w-full bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-3 text-xs text-[#0F172A] dark:text-slate-100 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-[#2563EB]"
              />
            </div>

            <div className="p-4 border-t border-[#E2E8F0] dark:border-[#334155] bg-[#F8FAFC] dark:bg-[#0F172A] flex justify-end gap-2">
              <button onClick={() => setSelectedOutcome(null)} className="px-4 py-2 rounded-xl bg-slate-200 dark:bg-slate-800 text-xs font-bold cursor-pointer">
                Cancel
              </button>
              <button
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
    </div>
  );
};
