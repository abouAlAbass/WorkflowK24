import React, { useState } from 'react';
import { Play, Plus, Trash2, FileSpreadsheet, RefreshCw } from 'lucide-react';
import type { WorkflowNodeCustomData, TriggerType, WorkflowVariable } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import { useSmartFormStore } from '../../store/useSmartFormStore';

interface TriggerConfigProps {
  nodeId: string;
  data: WorkflowNodeCustomData;
}

export const TriggerConfig: React.FC<TriggerConfigProps> = ({ nodeId, data }) => {
  const { updateNodeData, setCurrentTab } = useWorkflowStore();
  const { forms, selectForm } = useSmartFormStore();

  const trigger = data.triggerData || {
    label: 'Workflow Trigger',
    triggerType: 'form' as TriggerType,
    eventName: 'On_Start',
    variables: []
  };

  const [varName, setVarName] = useState('');
  const [varType, setVarType] = useState<'string' | 'number' | 'boolean' | 'date'>('string');
  const [varDefault, setVarDefault] = useState('');

  const handleTriggerTypeChange = (type: TriggerType) => {
    updateNodeData(nodeId, {
      triggerData: {
        ...trigger,
        triggerType: type
      }
    });
  };

  const handleAddVariable = (e: React.FormEvent) => {
    e.preventDefault();
    if (!varName.trim()) return;

    const newVar: WorkflowVariable = {
      name: varName.trim().replace(/\s+/g, '_'),
      type: varType,
      defaultValue: varDefault
    };

    updateNodeData(nodeId, {
      triggerData: {
        ...trigger,
        variables: [...(trigger.variables || []), newVar]
      }
    });

    setVarName('');
    setVarDefault('');
  };

  const handleRemoveVariable = (index: number) => {
    const updated = [...(trigger.variables || [])];
    updated.splice(index, 1);
    updateNodeData(nodeId, {
      triggerData: {
        ...trigger,
        variables: updated
      }
    });
  };

  const inputClass = "w-full px-3.5 py-2.5 rounded-xl text-xs bg-white dark:bg-slate-800 border border-slate-300 dark:border-slate-700 text-[#0F172A] dark:text-slate-100 placeholder-[#64748B] dark:placeholder-slate-400 font-semibold focus:outline-none focus:ring-2 focus:ring-[#2563EB] shadow-2xs transition-all";

  return (
    <div className="space-y-5 text-[#0F172A] dark:text-slate-100">
      <div className="space-y-1.5">
        <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200">Trigger Title / Label</label>
        <input
          type="text"
          value={data.label || ''}
          onChange={(e) => updateNodeData(nodeId, { label: e.target.value })}
          className={inputClass}
          placeholder="e.g. Employee Onboarding Form"
        />
      </div>

      <div className="space-y-1.5">
        <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200 flex items-center gap-1.5">
          <Play className="w-3.5 h-3.5 text-[#059669] dark:text-emerald-400" />
          <span>Trigger Event Type</span>
        </label>
        <select
          value={trigger.triggerType}
          onChange={(e) => handleTriggerTypeChange(e.target.value as TriggerType)}
          className={inputClass}
        >
          <option value="form">📋 Tamam Form Submission</option>
          <option value="schedule">⏰ Timer Schedule (Cron)</option>
          <option value="webhook">🌐 Webhook / API Listener</option>
          <option value="item_created">🗄️ Database Record Event</option>
        </select>
      </div>

      {trigger.triggerType === 'form' && (
        <div className="p-3 rounded-2xl border border-blue-200 dark:border-blue-900/60 bg-blue-50/40 dark:bg-blue-950/20 space-y-2.5">
          <div className="flex items-center justify-between">
            <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200 flex items-center gap-1.5">
              <FileSpreadsheet className="w-3.5 h-3.5 text-[#2563EB] dark:text-blue-400" />
              <span>Bound Start SmartForm</span>
            </label>
            {trigger.formId && (
              <button
                type="button"
                onClick={() => {
                  selectForm(trigger.formId!);
                  setCurrentTab('smartforms');
                }}
                className="text-[10px] font-bold text-[#2563EB] dark:text-blue-400 hover:underline cursor-pointer"
              >
                Edit Form →
              </button>
            )}
          </div>

          <select
            value={trigger.formId || ''}
            onChange={(e) =>
              updateNodeData(nodeId, {
                triggerData: { ...trigger, formId: e.target.value || undefined }
              })
            }
            className={inputClass}
          >
            <option value="">— Select a No-Code SmartForm —</option>
            {forms.map((f) => (
              <option key={f.id} value={f.id}>
                {f.title} ({f.fields.filter((x) => x.type !== 'section').length} fields)
              </option>
            ))}
          </select>

          {trigger.formId && (
            <button
              type="button"
              onClick={() => {
                const chosen = forms.find((f) => f.id === trigger.formId);
                if (!chosen) return;
                const syncedVars: WorkflowVariable[] = chosen.fields
                  .filter((fld) => fld.type !== 'section')
                  .map((fld) => ({
                    name: fld.name,
                    type:
                      fld.type === 'number' || fld.type === 'currency'
                        ? 'number'
                        : fld.type === 'checkbox'
                        ? 'boolean'
                        : fld.type === 'date'
                        ? 'date'
                        : 'string',
                    defaultValue: fld.defaultValue || ''
                  }));
                updateNodeData(nodeId, {
                  triggerData: {
                    ...trigger,
                    variables: syncedVars
                  }
                });
              }}
              className="w-full py-1.5 px-2.5 rounded-xl bg-white dark:bg-slate-800 border border-slate-300 dark:border-slate-700 text-[11px] font-bold text-[#2563EB] dark:text-blue-400 flex items-center justify-center gap-1.5 cursor-pointer hover:bg-blue-50 dark:hover:bg-slate-700"
            >
              <RefreshCw className="w-3 h-3" />
              <span>Sync Form Fields → Payload Variables</span>
            </button>
          )}
        </div>
      )}

      <div className="space-y-1.5">
        <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200">Event Identifier Name</label>
        <input
          type="text"
          value={trigger.eventName || ''}
          onChange={(e) => updateNodeData(nodeId, {
            triggerData: { ...trigger, eventName: e.target.value }
          })}
          className={`${inputClass} font-mono`}
          placeholder="Onboarding_Submit_V1"
        />
      </div>

      <div className="space-y-3 pt-2 border-t border-slate-200 dark:border-slate-800">
        <div className="flex items-center justify-between">
          <label className="text-xs font-bold text-[#0F172A] dark:text-slate-200">
            Payload Input Variables ({trigger.variables?.length || 0})
          </label>
          <span className="text-[10px] text-[#059669] dark:text-emerald-400 font-bold">Available downstream</span>
        </div>

        <div className="space-y-2 max-h-48 overflow-y-auto pr-1">
          {trigger.variables?.map((v, idx) => (
            <div
              key={idx}
              className="flex items-center justify-between p-2.5 rounded-xl bg-white dark:bg-slate-800 border border-slate-300 dark:border-slate-700 text-xs shadow-2xs"
            >
              <div>
                <span className="font-mono font-bold text-[#059669] dark:text-emerald-400">{`{${v.name}}`}</span>
                <span className="text-[10px] text-[#475569] dark:text-slate-400 font-medium ml-2">({v.type})</span>
                {v.defaultValue && (
                  <span className="text-[10px] text-[#475569] dark:text-slate-400 block font-mono">
                    Default: "{v.defaultValue}"
                  </span>
                )}
              </div>
              <button
                type="button"
                onClick={() => handleRemoveVariable(idx)}
                className="p-1 text-slate-400 hover:text-[#DC2626] dark:hover:text-rose-400 transition-colors cursor-pointer"
                title="Remove variable"
                aria-label={`Remove variable ${v.name}`}
              >
                <Trash2 className="w-3.5 h-3.5" />
              </button>
            </div>
          ))}
        </div>

        <form onSubmit={handleAddVariable} className="space-y-2 pt-1">
          <div className="flex gap-2">
            <input
              type="text"
              placeholder="Variable Name (e.g. EmployeeName)"
              value={varName}
              onChange={(e) => setVarName(e.target.value)}
              className={`flex-1 ${inputClass} font-mono`}
            />
            <select
              value={varType}
              onChange={(e) => setVarType(e.target.value as any)}
              className={`${inputClass} !w-28`}
            >
              <option value="string">string</option>
              <option value="number">number</option>
              <option value="boolean">boolean</option>
              <option value="date">date</option>
            </select>
          </div>

          <div className="flex gap-2">
            <input
              type="text"
              placeholder="Default Value (optional)"
              value={varDefault}
              onChange={(e) => setVarDefault(e.target.value)}
              className={`flex-1 ${inputClass}`}
            />
            <button
              type="submit"
              className="px-3.5 py-2 bg-[#059669] hover:bg-emerald-700 text-white rounded-xl text-xs font-bold flex items-center gap-1 shadow-sm transition-all cursor-pointer focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              <Plus className="w-3.5 h-3.5" />
              <span>Add Variable</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
