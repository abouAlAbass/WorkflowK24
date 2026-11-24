import React from 'react';
import { Layers, ArrowRight, Sparkles, FolderTree } from 'lucide-react';
import type { WorkflowNodeCustomData } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

interface StageConfigProps {
  nodeId: string;
  data: WorkflowNodeCustomData;
}

export const StageConfig: React.FC<StageConfigProps> = ({ nodeId, data }) => {
  const { updateNodeData, enterStage } = useWorkflowStore();
  const stage = data.stageData;

  const stageName = stage?.stageName || data.label || 'Workflow Stage';
  const description = stage?.description || '';
  const subNodes = stage?.subWorkflow?.nodes || [];
  const subEdges = stage?.subWorkflow?.edges || [];

  const handleNameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newName = e.target.value;
    updateNodeData(nodeId, {
      label: newName,
      stageData: {
        ...stage!,
        label: newName,
        stageName: newName
      }
    });
  };

  const handleDescChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    updateNodeData(nodeId, {
      stageData: {
        ...stage!,
        description: e.target.value
      }
    });
  };

  return (
    <div className="p-4 space-y-5 text-xs text-[#0F172A] dark:text-slate-100">
      {/* Primary Action Button: Drill Down */}
      <div className="p-4 rounded-2xl bg-gradient-to-br from-indigo-500 to-purple-600 text-white shadow-lg shadow-indigo-500/20 space-y-3">
        <div className="flex items-center gap-2">
          <div className="p-1.5 rounded-lg bg-white/20 backdrop-blur-xs">
            <Sparkles className="w-4 h-4 text-white" />
          </div>
          <div>
            <h4 className="font-extrabold text-xs">Stage Workflow Designer</h4>
            <p className="text-[11px] text-indigo-100">Encapsulated sub-process logic</p>
          </div>
        </div>

        <button
          type="button"
          onClick={() => enterStage(nodeId)}
          data-testid="stage-config-drilldown-btn"
          className="w-full py-2.5 px-3 bg-white hover:bg-indigo-50 text-indigo-700 font-extrabold rounded-xl shadow-md flex items-center justify-center gap-2 cursor-pointer transition-all active:scale-[0.98]"
        >
          <Layers className="w-4 h-4" />
          <span>Open & Design Stage Canvas</span>
          <ArrowRight className="w-4 h-4" />
        </button>
      </div>

      {/* Stage Metadata Form */}
      <div className="space-y-3">
        <div>
          <label className="block text-[11px] font-bold text-[#475569] dark:text-slate-300 mb-1">
            Stage Name
          </label>
          <input
            type="text"
            value={stageName}
            onChange={handleNameChange}
            data-testid="stage-name-input"
            className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs font-bold text-[#0F172A] dark:text-slate-100 focus:outline-none focus:border-indigo-500"
          />
        </div>

        <div>
          <label className="block text-[11px] font-bold text-[#475569] dark:text-slate-300 mb-1">
            Stage Business Description
          </label>
          <textarea
            rows={3}
            value={description}
            onChange={handleDescChange}
            placeholder="Describe what occurs inside this workflow stage..."
            data-testid="stage-description-input"
            className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs text-[#0F172A] dark:text-slate-100 focus:outline-none focus:border-indigo-500"
          />
        </div>
      </div>

      {/* Internal Sub-Process Statistics */}
      <div className="space-y-2 pt-2 border-t border-slate-200 dark:border-slate-800">
        <h4 className="text-[11px] font-extrabold uppercase tracking-wider text-[#475569] dark:text-slate-400 flex items-center gap-1.5">
          <FolderTree className="w-3.5 h-3.5 text-indigo-500" />
          <span>Sub-Process Architecture ({subNodes.length} nodes)</span>
        </h4>

        <div className="grid grid-cols-2 gap-2">
          <div className="p-2.5 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200 dark:border-slate-800 text-center">
            <span className="block text-lg font-black text-indigo-600 dark:text-indigo-400">
              {subNodes.length}
            </span>
            <span className="text-[10px] text-slate-500 font-medium">Internal Steps</span>
          </div>
          <div className="p-2.5 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200 dark:border-slate-800 text-center">
            <span className="block text-lg font-black text-indigo-600 dark:text-indigo-400">
              {subEdges.length}
            </span>
            <span className="text-[10px] text-slate-500 font-medium">Transitions</span>
          </div>
        </div>

        {subNodes.length > 0 && (
          <div className="space-y-1.5 pt-1">
            <span className="text-[10px] text-slate-400 font-bold uppercase tracking-wider">Internal Nodes Preview</span>
            <div className="space-y-1">
              {subNodes.map((n) => (
                <div key={n.id} className="p-2 rounded-lg bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 flex items-center justify-between text-[11px]">
                  <span className="font-bold truncate">{n.data?.label || n.id}</span>
                  <span className="text-[9px] font-mono uppercase px-1.5 py-0.5 rounded bg-slate-100 dark:bg-slate-800 text-slate-500">
                    {n.type}
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
