import React, { useEffect, useState } from 'react';
import { Globe, Plus, Trash2, ShieldAlert } from 'lucide-react';
import type { ConnectionRecord, HttpNodeData, WorkflowNodeCustomData } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import { getWorkflowRepository } from '../../services/repositoryManager';

interface HttpConfigProps {
  nodeId: string;
  data: WorkflowNodeCustomData;
}

const HTTP_METHODS: Array<HttpNodeData['method']> = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'];

export const HttpConfig: React.FC<HttpConfigProps> = ({ nodeId, data }) => {
  const { updateNodeData, nodes } = useWorkflowStore();
  const [connections, setConnections] = useState<ConnectionRecord[]>([]);
  const [newMapKey, setNewMapKey] = useState('');
  const [newMapPath, setNewMapPath] = useState('$.');

  const httpData: HttpNodeData = data.httpData || {
    label: data.label || 'HTTP Request',
    connectionRef: '',
    method: 'GET',
    path: '/',
    responseMapping: {},
    onBusinessError: { routeTo: '' }
  };

  useEffect(() => {
    let mounted = true;
    getWorkflowRepository()
      .getConnections()
      .then((list) => {
        if (mounted) setConnections(list);
      })
      .catch(() => {
        // Non-Admin/Designer users may not have access to /api/connections
      });
    return () => {
      mounted = false;
    };
  }, []);

  const updateHttp = (patch: Partial<HttpNodeData>) => {
    const merged: HttpNodeData = { ...httpData, ...patch };
    updateNodeData(nodeId, {
      httpData: merged,
      ...(patch.label !== undefined ? { label: patch.label } : {})
    });
  };

  const handleAddMapping = () => {
    const key = newMapKey.trim();
    const path = newMapPath.trim();
    if (!key || !path) return;
    updateHttp({
      responseMapping: {
        ...(httpData.responseMapping || {}),
        [key]: path
      }
    });
    setNewMapKey('');
    setNewMapPath('$.');
  };

  const handleRemoveMapping = (key: string) => {
    const next = { ...(httpData.responseMapping || {}) };
    delete next[key];
    updateHttp({ responseMapping: next });
  };

  const otherNodes = nodes.filter(n => n.id !== nodeId);

  return (
    <div className="space-y-4 text-xs text-start">
      <div className="flex items-center gap-2 pb-2 border-b border-slate-200 dark:border-slate-800">
        <Globe className="w-4 h-4 text-[#0891B2]" />
        <span className="font-bold text-[#0F172A] dark:text-slate-100">HTTP / REST Connector</span>
      </div>

      <div>
        <label htmlFor={`http-label-${nodeId}`} className="block font-bold text-[#0F172A] dark:text-slate-200 mb-1">
          Step Label
        </label>
        <input
          id={`http-label-${nodeId}`}
          type="text"
          value={data.label || ''}
          onChange={(e) => updateHttp({ label: e.target.value })}
          className="w-full bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-3 py-2 text-xs text-[#0F172A] dark:text-slate-100"
        />
      </div>

      <div>
        <label htmlFor={`http-conn-${nodeId}`} className="block font-bold text-[#0F172A] dark:text-slate-200 mb-1">
          Connection Reference (`connectionRef`)
        </label>
        {connections.length > 0 && (
          <select
            id={`http-conn-select-${nodeId}`}
            value={connections.some(c => c.name === httpData.connectionRef || c.id === httpData.connectionRef) ? httpData.connectionRef : ''}
            onChange={(e) => {
              if (e.target.value) updateHttp({ connectionRef: e.target.value });
            }}
            className="w-full mb-1.5 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-3 py-2 text-xs text-[#0F172A] dark:text-slate-100 font-semibold"
          >
            <option value="">-- Select saved connection --</option>
            {connections.map((c) => (
              <option key={c.id} value={c.name}>
                {c.name} ({c.baseUrl})
              </option>
            ))}
          </select>
        )}
        <input
          id={`http-conn-${nodeId}`}
          type="text"
          value={httpData.connectionRef || ''}
          onChange={(e) => updateHttp({ connectionRef: e.target.value })}
          placeholder="e.g. ERP_REST_API"
          className="w-full bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-3 py-2 text-xs font-mono text-[#0F172A] dark:text-slate-100"
        />
      </div>

      <div className="grid grid-cols-3 gap-2">
        <div>
          <label htmlFor={`http-method-${nodeId}`} className="block font-bold text-[#0F172A] dark:text-slate-200 mb-1">
            Method
          </label>
          <select
            id={`http-method-${nodeId}`}
            value={httpData.method || 'GET'}
            onChange={(e) => updateHttp({ method: e.target.value as HttpNodeData['method'] })}
            className="w-full bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-2.5 py-2 text-xs font-mono font-bold text-[#0F172A] dark:text-slate-100"
          >
            {HTTP_METHODS.map((m) => (
              <option key={m} value={m}>{m}</option>
            ))}
          </select>
        </div>

        <div className="col-span-2">
          <label htmlFor={`http-path-${nodeId}`} className="block font-bold text-[#0F172A] dark:text-slate-200 mb-1">
            Path (supports &#123;&#123;vars.x&#125;&#125;)
          </label>
          <input
            id={`http-path-${nodeId}`}
            type="text"
            value={httpData.path || ''}
            onChange={(e) => updateHttp({ path: e.target.value })}
            placeholder="/v1/orders/{{vars.orderId}}"
            className="w-full bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-3 py-2 text-xs font-mono text-[#0F172A] dark:text-slate-100"
          />
        </div>
      </div>

      <div className="p-3 rounded-xl bg-amber-50/70 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800/70 space-y-2">
        <div className="flex items-center gap-1.5 font-bold text-amber-900 dark:text-amber-300">
          <ShieldAlert className="w-3.5 h-3.5" />
          <span>On Business Error (4xx)</span>
        </div>
        <div>
          <label htmlFor={`http-biz-route-${nodeId}`} className="block font-semibold text-[#475569] dark:text-slate-300 mb-1">
            Route To Node (`onBusinessError.routeTo`)
          </label>
          <select
            id={`http-biz-route-${nodeId}`}
            value={httpData.onBusinessError?.routeTo || ''}
            onChange={(e) =>
              updateHttp({
                onBusinessError: {
                  ...(httpData.onBusinessError || {}),
                  routeTo: e.target.value || undefined
                }
              })
            }
            className="w-full bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-2.5 py-2 text-xs text-[#0F172A] dark:text-slate-100"
          >
            <option value="">(Fault instance if 4xx)</option>
            {otherNodes.map((n) => (
              <option key={n.id} value={n.id}>
                {(n.data as unknown as WorkflowNodeCustomData)?.label || n.id} ({n.id})
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="space-y-2 pt-1 border-t border-slate-200 dark:border-slate-800">
        <span className="block font-bold text-[#0F172A] dark:text-slate-200">
          Response Mapping (JSONPath)
        </span>
        {Object.entries(httpData.responseMapping || {}).map(([varName, jsonPath]) => (
          <div key={varName} className="flex items-center justify-between gap-2 px-2.5 py-1.5 rounded-lg bg-slate-100 dark:bg-slate-800 font-mono text-[11px]">
            <span className="truncate">
              <strong>{varName}</strong> &lt;- {jsonPath}
            </span>
            <button
              type="button"
              onClick={() => handleRemoveMapping(varName)}
              className="text-slate-400 hover:text-rose-600 cursor-pointer"
              aria-label={`Remove mapping ${varName}`}
            >
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          </div>
        ))}
        <div className="grid grid-cols-5 gap-1.5">
          <input
            id={`http-map-var-${nodeId}`}
            type="text"
            value={newMapKey}
            onChange={(e) => setNewMapKey(e.target.value)}
            placeholder="varName"
            aria-label="Target variable name"
            className="col-span-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg px-2 py-1.5 text-[11px] font-mono"
          />
          <input
            id={`http-map-path-${nodeId}`}
            type="text"
            value={newMapPath}
            onChange={(e) => setNewMapPath(e.target.value)}
            placeholder="$.data.id"
            aria-label="JSONPath expression"
            className="col-span-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg px-2 py-1.5 text-[11px] font-mono"
          />
          <button
            type="button"
            onClick={handleAddMapping}
            className="flex items-center justify-center rounded-lg bg-[#0891B2] hover:bg-cyan-700 text-white cursor-pointer"
            aria-label="Add response mapping"
          >
            <Plus className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>
    </div>
  );
};
