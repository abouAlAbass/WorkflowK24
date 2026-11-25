import React, { useState } from 'react';
import { 
  UserCheck, 
  Users, 
  Plus, 
  Trash2, 
  Sliders, 
  Info,
  FileSpreadsheet
} from 'lucide-react';
import type { WorkflowNodeCustomData, ApprovalOutcome } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import { useSmartFormStore } from '../../store/useSmartFormStore';

interface ApprovalConfigProps {
  nodeId: string;
  data: WorkflowNodeCustomData;
}

const PRESET_USERS = [
  'mohamed.fetni@company.com',
  'adel.sebti@company.com',
  'karim.khmissi@company.com',
  'yacine.benali@company.com',
  'rachid.boudiaf@company.com'
];

const PRESET_GROUPS = [
  'Executive Board',
  'Finance Managers',
  'Engineering Managers',
  'HR Admins',
  'IT Helpdesk'
];

const COLOR_PRESETS = [
  '#059669',
  '#DC2626',
  '#D97706',
  '#2563EB',
  '#7C3AED',
  '#DB2777',
];

export const ApprovalConfig: React.FC<ApprovalConfigProps> = ({ nodeId, data }) => {
  const { 
    updateNodeData, 
    addOutcomeToApproval, 
    removeOutcomeFromApproval, 
    updateOutcomeInApproval
  } = useWorkflowStore();
  const forms = useSmartFormStore((s) => s.forms);

  const approval = data.approvalData || {
    label: 'Approval Task',
    assignmentType: 'user',
    assignee: '',
    outcomes: [],
    allowReassignment: true,
    dueDateDays: 2
  };

  const [newOutcomeLabel, setNewOutcomeLabel] = useState('');
  const [newOutcomeColor, setNewOutcomeColor] = useState('#2563EB');

  const handleAssignmentTypeChange = (type: 'user' | 'group') => {
    updateNodeData(nodeId, {
      approvalData: {
        ...approval,
        assignmentType: type,
        assignee: type === 'user' ? PRESET_USERS[0] : PRESET_GROUPS[0]
      }
    });
  };

  const handleAssigneeChange = (val: string) => {
    updateNodeData(nodeId, {
      approvalData: {
        ...approval,
        assignee: val
      }
    });
  };

  const handleAddOutcome = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newOutcomeLabel.trim()) return;

    const newOutcome: ApprovalOutcome = {
      id: `outcome-${Date.now().toString().slice(-4)}`,
      label: newOutcomeLabel.trim(),
      color: newOutcomeColor
    };

    addOutcomeToApproval(nodeId, newOutcome);
    setNewOutcomeLabel('');
  };

  const inputClass = "w-full px-3.5 py-2.5 rounded-xl text-xs bg-white dark:bg-slate-800 border border-slate-300 dark:border-slate-700 text-[#0F172A] dark:text-slate-100 placeholder-[#64748B] dark:placeholder-slate-400 font-semibold focus:outline-none focus:ring-2 focus:ring-[#2563EB] shadow-2xs transition-all";

  return (
    <div className="space-y-5 text-[#0F172A] dark:text-slate-100">
      <div className="space-y-1.5">
        <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200">Task Title / Label</label>
        <input
          type="text"
          value={data.label || ''}
          onChange={(e) => updateNodeData(nodeId, { label: e.target.value })}
          className={inputClass}
          placeholder="e.g. Department Head Approval"
        />
      </div>

      <div className="space-y-2">
        <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200 flex items-center gap-1.5">
          <Sliders className="w-3.5 h-3.5 text-[#2563EB] dark:text-blue-400" />
          <span>Assignment Type</span>
        </label>
        <div className="grid grid-cols-2 gap-2 p-1 rounded-xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 shadow-2xs">
          <button
            type="button"
            onClick={() => handleAssignmentTypeChange('user')}
            className={`flex items-center justify-center gap-2 py-2 rounded-lg text-xs font-bold transition-all cursor-pointer ${
              approval.assignmentType === 'user'
                ? 'bg-[#2563EB] text-white shadow-sm'
                : 'text-[#475569] dark:text-slate-300 hover:text-[#0F172A] dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-700'
            }`}
          >
            <UserCheck className="w-3.5 h-3.5" />
            <span>Specific User</span>
          </button>
          <button
            type="button"
            onClick={() => handleAssignmentTypeChange('group')}
            className={`flex items-center justify-center gap-2 py-2 rounded-lg text-xs font-bold transition-all cursor-pointer ${
              approval.assignmentType === 'group'
                ? 'bg-[#7C3AED] text-white shadow-sm'
                : 'text-[#475569] dark:text-slate-300 hover:text-[#0F172A] dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-700'
            }`}
          >
            <Users className="w-3.5 h-3.5" />
            <span>Group / Role</span>
          </button>
        </div>
      </div>

      <div className="space-y-1.5">
        <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200">
          {approval.assignmentType === 'user' ? 'Assignee Email Address' : 'Assigned Role / Group'}
        </label>
        
        <input
          type="text"
          list="assignee-options"
          value={approval.assignee || ''}
          onChange={(e) => handleAssigneeChange(e.target.value)}
          placeholder={approval.assignmentType === 'user' ? 'mohamed.fetni@company.com' : 'Finance Managers'}
          className={`${inputClass} font-mono`}
        />

        <datalist id="assignee-options">
          {(approval.assignmentType === 'user' ? PRESET_USERS : PRESET_GROUPS).map((item) => (
            <option key={item} value={item} />
          ))}
        </datalist>

        <div className="flex flex-wrap gap-1.5 pt-1">
          <span className="text-[10px] text-[#475569] dark:text-slate-400 font-medium">Quick Select:</span>
          {(approval.assignmentType === 'user' ? PRESET_USERS.slice(0, 3) : PRESET_GROUPS.slice(0, 3)).map((item) => (
            <button
              key={item}
              type="button"
              onClick={() => handleAssigneeChange(item)}
              className="text-[10px] bg-white dark:bg-slate-800 hover:bg-slate-100 dark:hover:bg-slate-700 text-[#0F172A] dark:text-slate-200 px-2 py-0.5 rounded-md border border-slate-300 dark:border-slate-700 font-semibold shadow-2xs transition-colors cursor-pointer"
            >
              {item}
            </button>
          ))}
        </div>
      </div>

      <div className="space-y-3 pt-2 border-t border-slate-200 dark:border-slate-800">
        <div className="flex items-center justify-between">
          <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200">
            Dynamic Decision Outcomes ({approval.outcomes?.length || 0})
          </label>
          <span className="text-[10px] text-[#2563EB] dark:text-blue-400 font-bold">Outbound Handles</span>
        </div>

        <div className="space-y-2 max-h-56 overflow-y-auto pr-1">
          {approval.outcomes?.map((outcome) => (
            <div
              key={outcome.id}
              className="flex items-center gap-2 p-2.5 rounded-xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 shadow-2xs"
            >
              <input
                type="color"
                value={outcome.color}
                onChange={(e) => updateOutcomeInApproval(nodeId, outcome.id, { color: e.target.value })}
                className="w-6 h-6 rounded cursor-pointer border-0 bg-transparent shrink-0"
                title="Change handle badge color"
                aria-label={`Change color for ${outcome.label}`}
              />

              <input
                type="text"
                value={outcome.label}
                onChange={(e) => updateOutcomeInApproval(nodeId, outcome.id, { label: e.target.value })}
                className="flex-1 px-2.5 py-1 rounded-lg text-xs bg-slate-50 dark:bg-slate-900 border border-slate-300 dark:border-slate-700 text-[#0F172A] dark:text-slate-100 font-bold focus:outline-none focus:ring-2 focus:ring-[#2563EB]"
              />

              <button
                type="button"
                onClick={() => removeOutcomeFromApproval(nodeId, outcome.id)}
                disabled={approval.outcomes.length <= 1}
                className="p-1 rounded text-slate-400 hover:text-[#DC2626] dark:hover:text-rose-400 hover:bg-rose-50 dark:hover:bg-rose-950/40 disabled:opacity-30 disabled:cursor-not-allowed transition-colors cursor-pointer"
                title="Remove outcome handle"
                aria-label={`Remove outcome ${outcome.label}`}
              >
                <Trash2 className="w-3.5 h-3.5" />
              </button>
            </div>
          ))}
        </div>

        <form onSubmit={handleAddOutcome} className="flex gap-2 pt-1">
          <input
            type="text"
            placeholder="New outcome (e.g. Delegate)"
            value={newOutcomeLabel}
            onChange={(e) => setNewOutcomeLabel(e.target.value)}
            className={`flex-1 ${inputClass}`}
          />

          <div className="flex items-center gap-1">
            {COLOR_PRESETS.slice(0, 3).map((col) => (
              <button
                key={col}
                type="button"
                onClick={() => setNewOutcomeColor(col)}
                className={`w-5 h-5 rounded-full border border-white dark:border-slate-800 cursor-pointer ${newOutcomeColor === col ? 'ring-2 ring-[#2563EB] scale-110' : ''}`}
                style={{ backgroundColor: col }}
                aria-label={`Color preset ${col}`}
              />
            ))}
          </div>

          <button
            type="submit"
            className="px-3.5 py-2 bg-[#2563EB] hover:bg-blue-700 text-white rounded-xl text-xs font-bold flex items-center gap-1 shadow-sm transition-all cursor-pointer focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <Plus className="w-3.5 h-3.5" />
            <span>Add</span>
          </button>
        </form>
      </div>

      <div className="space-y-2 pt-2 border-t border-slate-200 dark:border-slate-800">
        <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200">Task SLA & Rules</label>
        
        <div className="flex items-center justify-between p-2.5 rounded-xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 text-xs shadow-2xs">
          <span className="text-[#475569] dark:text-slate-300 font-medium">Due Date SLA (Days):</span>
          <input
            type="number"
            min={1}
            max={30}
            value={approval.dueDateDays || 2}
            onChange={(e) => updateNodeData(nodeId, {
              approvalData: { ...approval, dueDateDays: parseInt(e.target.value) || 2 }
            })}
            className="w-16 px-2 py-1 rounded-lg text-center text-xs font-bold bg-slate-50 dark:bg-slate-900 border border-slate-300 dark:border-slate-700 text-[#0F172A] dark:text-slate-100"
          />
        </div>

        <label className="flex items-center gap-2 p-2.5 rounded-xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 text-xs cursor-pointer text-[#0F172A] dark:text-slate-100 font-semibold shadow-2xs">
          <input
            type="checkbox"
            checked={approval.allowReassignment ?? true}
            onChange={(e) => updateNodeData(nodeId, {
              approvalData: { ...approval, allowReassignment: e.target.checked }
            })}
            className="rounded border-slate-300 dark:border-slate-700 text-[#2563EB] focus:ring-[#2563EB]"
          />
          <span>Allow Task Reassignment by Assignee</span>
        </label>
      </div>

      <div className="space-y-2 pt-2 border-t border-slate-200 dark:border-slate-800">
        <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200 flex items-center gap-1.5">
          <FileSpreadsheet className="w-3.5 h-3.5 text-[#2563EB] dark:text-blue-400" />
          <span>Task Review SmartForm</span>
        </label>

        <select
          value={approval.formId || ''}
          onChange={(e) =>
            updateNodeData(nodeId, {
              approvalData: { ...approval, formId: e.target.value || undefined }
            })
          }
          className={inputClass}
        >
          <option value="">— Default Task Card (No SmartForm) —</option>
          {forms.map((f) => (
            <option key={f.id} value={f.id}>
              {f.title} ({f.category})
            </option>
          ))}
        </select>

        {approval.formId && (
          <select
            value={approval.formMode || 'readonly'}
            onChange={(e) =>
              updateNodeData(nodeId, {
                approvalData: { ...approval, formMode: e.target.value as 'readonly' | 'editable' }
              })
            }
            className={inputClass}
          >
            <option value="readonly">🔒 Read-Only Consultation for Approver</option>
            <option value="editable">✏️ Editable Form Fields for Approver</option>
          </select>
        )}
      </div>

      <div className="p-3 rounded-xl bg-blue-50 dark:bg-blue-950/40 border border-blue-200 dark:border-blue-900/60 text-[11px] text-[#475569] dark:text-slate-300 flex items-start gap-2 font-medium">
        <Info className="w-4 h-4 text-[#2563EB] dark:text-blue-400 shrink-0 mt-0.5" />
        <span>
          Each choice dynamically generates an outbound source handle. Multiple outcome handles can connect independently or merge into a single downstream action.
        </span>
      </div>
    </div>
  );
};
