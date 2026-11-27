import React, { useEffect, useMemo, useState } from 'react';
import { Database, Plus, Trash2, ShieldAlert, ArrowRightLeft } from 'lucide-react';
import type {
  SmartObjectEntity,
  SmartObjectMethodContract,
  SmartObjectNodeData,
  WorkflowNodeCustomData
} from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import { getWorkflowRepository } from '../../services/repositoryManager';

interface SmartObjectConfigProps {
  nodeId: string;
  data: WorkflowNodeCustomData;
}

export const SmartObjectConfig: React.FC<SmartObjectConfigProps> = ({ nodeId, data }) => {
  const { updateNodeData, nodes } = useWorkflowStore();
  const [smartObjects, setSmartObjects] = useState<SmartObjectEntity[]>([]);
  const [newInputKey, setNewInputKey] = useState('');
  const [newInputValue, setNewInputValue] = useState('');
  const [newOutputVar, setNewOutputVar] = useState('');
  const [newOutputField, setNewOutputField] = useState('');

  const soData: SmartObjectNodeData = data.smartObjectData || {
    label: data.label || 'SmartObject Action',
    smartObjectRef: '',
    methodName: '',
    inputMapping: {},
    outputMapping: {},
    onBusinessError: { routeTo: '' }
  };

  useEffect(() => {
    let mounted = true;
    getWorkflowRepository()
      .getSmartObjects()
      .then((list) => {
        if (mounted) setSmartObjects(list);
      })
      .catch(() => {
        // Ignore if user cannot list SmartObjects
      });
    return () => {
      mounted = false;
    };
  }, []);

  const getObjectRef = (so: SmartObjectEntity): string =>
    so.objectRef || `${so.name}@${so.version ?? 1}`;

  const selectedSmartObject = useMemo(
    () => smartObjects.find((so) => getObjectRef(so).toLowerCase() === (soData.smartObjectRef || '').toLowerCase()),
    [smartObjects, soData.smartObjectRef]
  );

  const availableMethods: SmartObjectMethodContract[] = selectedSmartObject?.methods || [];
  const selectedMethod = useMemo(
    () => availableMethods.find((m) => m.name.toLowerCase() === (soData.methodName || '').toLowerCase()),
    [availableMethods, soData.methodName]
  );

  const updateSo = (patch: Partial<SmartObjectNodeData>) => {
    const merged: SmartObjectNodeData = { ...soData, ...patch };
    updateNodeData(nodeId, {
      smartObjectData: merged,
      ...(patch.label !== undefined ? { label: patch.label } : {})
    });
  };

  const handleSelectSmartObject = (objectRef: string) => {
    const matched = smartObjects.find((so) => getObjectRef(so) === objectRef);
    const firstMethod = matched?.methods?.[0];
    const initialInputs: Record<string, unknown> = {};
    if (firstMethod?.inputs) {
      for (const inp of firstMethod.inputs) {
        if (inp.required) {
          initialInputs[inp.name] = `{{vars.${inp.name}}}`;
        }
      }
    }
    updateSo({
      smartObjectRef: objectRef,
      methodName: firstMethod?.name || '',
      inputMapping: firstMethod ? initialInputs : soData.inputMapping || {}
    });
  };

  const handleSelectMethod = (methodName: string) => {
    const matched = availableMethods.find((m) => m.name === methodName);
    const nextInputs: Record<string, unknown> = { ...(soData.inputMapping || {}) };
    if (matched?.inputs) {
      for (const inp of matched.inputs) {
        if (inp.required && !(inp.name in nextInputs)) {
          nextInputs[inp.name] = `{{vars.${inp.name}}}`;
        }
      }
    }
    updateSo({ methodName, inputMapping: nextInputs });
  };

  const handleSetInputValue = (key: string, value: string) => {
    updateSo({
      inputMapping: {
        ...(soData.inputMapping || {}),
        [key]: value
      }
    });
  };

  const handleAddInput = () => {
    const k = newInputKey.trim();
    if (!k) return;
    handleSetInputValue(k, newInputValue);
    setNewInputKey('');
    setNewInputValue('');
  };

  const handleRemoveInput = (key: string) => {
    const next = { ...(soData.inputMapping || {}) };
    delete next[key];
    updateSo({ inputMapping: next });
  };

  const handleAddOutput = () => {
    const varName = newOutputVar.trim();
    const fieldName = newOutputField.trim();
    if (!varName || !fieldName) return;
    updateSo({
      outputMapping: {
        ...(soData.outputMapping || {}),
        [varName]: fieldName
      }
    });
    setNewOutputVar('');
    setNewOutputField('');
  };

  const handleRemoveOutput = (varName: string) => {
    const next = { ...(soData.outputMapping || {}) };
    delete next[varName];
    updateSo({ outputMapping: next });
  };

  const otherNodes = nodes.filter((n) => n.id !== nodeId);

  return (
    <div className="space-y-4 text-xs text-start">
      <div className="flex items-center gap-2 pb-2 border-b border-slate-200 dark:border-slate-800">
        <Database className="w-4 h-4 text-[#0D9488]" />
        <span className="font-bold text-[#0F172A] dark:text-slate-100">SmartObject Action</span>
      </div>

      <div>
        <label htmlFor={`so-label-${nodeId}`} className="block font-bold text-[#0F172A] dark:text-slate-200 mb-1">
          Step Label
        </label>
        <input
          id={`so-label-${nodeId}`}
          type="text"
          value={data.label || ''}
          onChange={(e) => updateSo({ label: e.target.value })}
          className="w-full bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-3 py-2 text-xs text-[#0F172A] dark:text-slate-100"
        />
      </div>

      <div>
        <label htmlFor={`so-ref-${nodeId}`} className="block font-bold text-[#0F172A] dark:text-slate-200 mb-1">
          SmartObject@Version
        </label>
        {smartObjects.length > 0 && (
          <select
            id={`so-ref-select-${nodeId}`}
            value={selectedSmartObject ? getObjectRef(selectedSmartObject) : ''}
            onChange={(e) => {
              if (e.target.value) handleSelectSmartObject(e.target.value);
            }}
            className="w-full mb-1.5 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-3 py-2 text-xs text-[#0F172A] dark:text-slate-100 font-semibold"
          >
            <option value="">-- Select versioned SmartObject --</option>
            {smartObjects.map((so) => {
              const ref = getObjectRef(so);
              return (
                <option key={so.id} value={ref}>
                  {ref} ({so.status || 'Draft'}) - {so.displayName}
                </option>
              );
            })}
          </select>
        )}
        <input
          id={`so-ref-${nodeId}`}
          type="text"
          value={soData.smartObjectRef || ''}
          onChange={(e) => updateSo({ smartObjectRef: e.target.value })}
          placeholder="e.g. Customer@2"
          className="w-full bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-3 py-2 text-xs font-mono text-[#0F172A] dark:text-slate-100"
        />
      </div>

      <div>
        <label htmlFor={`so-method-${nodeId}`} className="block font-bold text-[#0F172A] dark:text-slate-200 mb-1">
          Method
        </label>
        {availableMethods.length > 0 && (
          <select
            id={`so-method-select-${nodeId}`}
            value={selectedMethod ? selectedMethod.name : ''}
            onChange={(e) => {
              if (e.target.value) handleSelectMethod(e.target.value);
            }}
            className="w-full mb-1.5 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-3 py-2 text-xs font-mono font-semibold text-[#0F172A] dark:text-slate-100"
          >
            <option value="">-- Select contract method --</option>
            {availableMethods.map((m) => (
              <option key={m.name} value={m.name}>
                {m.name} [{m.kind}] {m.idempotent ? '(idempotent)' : '(non-idempotent)'}
              </option>
            ))}
          </select>
        )}
        <input
          id={`so-method-${nodeId}`}
          type="text"
          value={soData.methodName || ''}
          onChange={(e) => updateSo({ methodName: e.target.value })}
          placeholder="e.g. GetById"
          className="w-full bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-3 py-2 text-xs font-mono text-[#0F172A] dark:text-slate-100"
        />
      </div>

      {/* Inputs Mapping */}
      <div className="space-y-2 pt-2 border-t border-slate-200 dark:border-slate-800">
        <div className="flex items-center gap-1.5 font-bold text-[#0F172A] dark:text-slate-200">
          <ArrowRightLeft className="w-3.5 h-3.5 text-[#0D9488]" />
          <span>Input Mapping (supports &#123;&#123;vars.x&#125;&#125;)</span>
        </div>

        {selectedMethod?.inputs && selectedMethod.inputs.length > 0 && (
          <div className="space-y-1.5">
            {selectedMethod.inputs.map((param) => {
              const currentVal = soData.inputMapping?.[param.name];
              return (
                <div key={param.name}>
                  <label
                    htmlFor={`so-inp-${nodeId}-${param.name}`}
                    className="block text-[11px] font-mono font-semibold text-[#475569] dark:text-slate-300 mb-0.5"
                  >
                    {param.name} ({param.type}){param.required ? ' *' : ''}
                  </label>
                  <input
                    id={`so-inp-${nodeId}-${param.name}`}
                    type="text"
                    value={currentVal === undefined || currentVal === null ? '' : String(currentVal)}
                    onChange={(e) => handleSetInputValue(param.name, e.target.value)}
                    placeholder={`{{vars.${param.name}}}`}
                    className="w-full bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg px-2.5 py-1.5 text-[11px] font-mono text-[#0F172A] dark:text-slate-100"
                  />
                </div>
              );
            })}
          </div>
        )}

        {Object.entries(soData.inputMapping || {})
          .filter(([k]) => !(selectedMethod?.inputs || []).some((p) => p.name === k))
          .map(([k, v]) => (
            <div key={k} className="flex items-center justify-between gap-2 px-2.5 py-1.5 rounded-lg bg-slate-100 dark:bg-slate-800 font-mono text-[11px]">
              <span className="truncate">
                <strong>{k}</strong> = {String(v ?? '')}
              </span>
              <button
                type="button"
                onClick={() => handleRemoveInput(k)}
                className="text-slate-400 hover:text-rose-600 cursor-pointer"
                aria-label={`Remove input ${k}`}
              >
                <Trash2 className="w-3.5 h-3.5" />
              </button>
            </div>
          ))}

        <div className="grid grid-cols-5 gap-1.5">
          <input
            id={`so-add-inp-key-${nodeId}`}
            type="text"
            value={newInputKey}
            onChange={(e) => setNewInputKey(e.target.value)}
            placeholder="inputName"
            aria-label="Input parameter name"
            className="col-span-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg px-2 py-1.5 text-[11px] font-mono"
          />
          <input
            id={`so-add-inp-val-${nodeId}`}
            type="text"
            value={newInputValue}
            onChange={(e) => setNewInputValue(e.target.value)}
            placeholder="{{vars.id}}"
            aria-label="Input value or template"
            className="col-span-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg px-2 py-1.5 text-[11px] font-mono"
          />
          <button
            type="button"
            onClick={handleAddInput}
            className="flex items-center justify-center rounded-lg bg-[#0D9488] hover:bg-teal-700 text-white cursor-pointer"
            aria-label="Add input mapping"
          >
            <Plus className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      {/* Outputs Mapping */}
      <div className="space-y-2 pt-2 border-t border-slate-200 dark:border-slate-800">
        <span className="block font-bold text-[#0F172A] dark:text-slate-200">
          Output Mapping (Variable &lt;- Output Field)
        </span>
        {Object.entries(soData.outputMapping || {}).map(([varName, outField]) => (
          <div key={varName} className="flex items-center justify-between gap-2 px-2.5 py-1.5 rounded-lg bg-slate-100 dark:bg-slate-800 font-mono text-[11px]">
            <span className="truncate">
              <strong>{varName}</strong> &lt;- {outField}
            </span>
            <button
              type="button"
              onClick={() => handleRemoveOutput(varName)}
              className="text-slate-400 hover:text-rose-600 cursor-pointer"
              aria-label={`Remove output mapping ${varName}`}
            >
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          </div>
        ))}
        <div className="grid grid-cols-5 gap-1.5">
          <input
            id={`so-out-var-${nodeId}`}
            type="text"
            value={newOutputVar}
            onChange={(e) => setNewOutputVar(e.target.value)}
            placeholder="varName"
            aria-label="Target workflow variable name"
            className="col-span-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg px-2 py-1.5 text-[11px] font-mono"
          />
          {selectedMethod?.outputs && selectedMethod.outputs.length > 0 ? (
            <select
              id={`so-out-field-${nodeId}`}
              value={newOutputField}
              onChange={(e) => setNewOutputField(e.target.value)}
              aria-label="SmartObject output field"
              className="col-span-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg px-2 py-1.5 text-[11px] font-mono"
            >
              <option value="">output...</option>
              {selectedMethod.outputs.map((o) => (
                <option key={o.name} value={o.name}>
                  {o.name} ({o.type})
                </option>
              ))}
            </select>
          ) : (
            <input
              id={`so-out-field-${nodeId}`}
              type="text"
              value={newOutputField}
              onChange={(e) => setNewOutputField(e.target.value)}
              placeholder="outputField"
              aria-label="SmartObject output field"
              className="col-span-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg px-2 py-1.5 text-[11px] font-mono"
            />
          )}
          <button
            type="button"
            onClick={handleAddOutput}
            className="flex items-center justify-center rounded-lg bg-[#0D9488] hover:bg-teal-700 text-white cursor-pointer"
            aria-label="Add output mapping"
          >
            <Plus className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      {/* Business Error Routing */}
      <div className="p-3 rounded-xl bg-amber-50/70 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800/70 space-y-2">
        <div className="flex items-center gap-1.5 font-bold text-amber-900 dark:text-amber-300">
          <ShieldAlert className="w-3.5 h-3.5" />
          <span>On Business Error (NOT_FOUND, VALIDATION, CONFLICT)</span>
        </div>
        <div>
          <label htmlFor={`so-biz-route-${nodeId}`} className="block font-semibold text-[#475569] dark:text-slate-300 mb-1">
            Route To Node (`onBusinessError.routeTo`)
          </label>
          <select
            id={`so-biz-route-${nodeId}`}
            value={soData.onBusinessError?.routeTo || ''}
            onChange={(e) =>
              updateSo({
                onBusinessError: {
                  ...(soData.onBusinessError || {}),
                  routeTo: e.target.value || undefined
                }
              })
            }
            className="w-full bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-2.5 py-2 text-xs text-[#0F172A] dark:text-slate-100"
          >
            <option value="">(Fault instance on business error)</option>
            {otherNodes.map((n) => (
              <option key={n.id} value={n.id}>
                {(n.data as unknown as WorkflowNodeCustomData)?.label || n.id} ({n.id})
              </option>
            ))}
          </select>
        </div>
      </div>
    </div>
  );
};
