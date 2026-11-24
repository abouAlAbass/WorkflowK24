import React from 'react';
import { 
  Play, 
  CheckCircle2, 
  Mail, 
  Flag, 
  GitBranch, 
  GripVertical, 
  FolderKanban, 
  Plus,
  Layers
} from 'lucide-react';
import type { NodeType } from '../../types/workflow';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import { useTheme } from '../../hooks/useTheme';

interface NodePaletteItem {
  type: NodeType;
  label: string;
  category: 'Triggers' | 'Tasks' | 'Notifications' | 'Logic' | 'Stages' | 'End';
  description: string;
  icon: React.ReactNode;
  colorClass: string;
  badgeBg: string;
}

const PALETTE_ITEMS: NodePaletteItem[] = [
  {
    type: 'trigger',
    label: 'Start / Trigger',
    category: 'Triggers',
    description: 'Defines event starting the workflow',
    icon: <Play className="w-3.5 h-3.5 text-[#059669]" />,
    colorClass: 'border-emerald-200 dark:border-emerald-800/80 hover:border-emerald-400 bg-white dark:bg-slate-800',
    badgeBg: 'bg-emerald-50 dark:bg-emerald-950/60 text-[#059669] dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800'
  },
  {
    type: 'approval',
    label: 'Approval Task',
    category: 'Tasks',
    description: 'User or Group decision task with dynamic outcome handles',
    icon: <CheckCircle2 className="w-3.5 h-3.5 text-[#2563EB]" />,
    colorClass: 'border-blue-200 dark:border-blue-800/80 hover:border-blue-400 bg-white dark:bg-slate-800',
    badgeBg: 'bg-blue-50 dark:bg-blue-950/60 text-[#2563EB] dark:text-blue-400 border border-blue-200 dark:border-blue-800'
  },
  {
    type: 'stage',
    label: 'Workflow Stage',
    category: 'Stages',
    description: 'Composite sub-process with isolated workflow logic & canvas',
    icon: <Layers className="w-3.5 h-3.5 text-[#6366F1]" />,
    colorClass: 'border-indigo-200 dark:border-indigo-800/80 hover:border-indigo-400 bg-white dark:bg-slate-800',
    badgeBg: 'bg-indigo-50 dark:bg-indigo-950/60 text-[#6366F1] dark:text-indigo-400 border border-indigo-200 dark:border-indigo-800'
  },
  {
    type: 'sendEmail',
    label: 'Send Email',
    category: 'Notifications',
    description: 'Send automated email notification',
    icon: <Mail className="w-3.5 h-3.5 text-[#D97706]" />,
    colorClass: 'border-amber-200 dark:border-amber-800/80 hover:border-amber-400 bg-white dark:bg-slate-800',
    badgeBg: 'bg-amber-50 dark:bg-amber-950/60 text-[#D97706] dark:text-amber-400 border border-amber-200 dark:border-amber-800'
  },
  {
    type: 'decision',
    label: 'Decision / Rule',
    category: 'Logic',
    description: 'Conditional logical branching (IF / THEN)',
    icon: <GitBranch className="w-3.5 h-3.5 text-[#7C3AED]" />,
    colorClass: 'border-purple-200 dark:border-purple-800/80 hover:border-purple-400 bg-white dark:bg-slate-800',
    badgeBg: 'bg-purple-50 dark:bg-purple-950/60 text-[#7C3AED] dark:text-purple-400 border border-purple-200 dark:border-purple-800'
  },
  {
    type: 'end',
    label: 'End Event',
    category: 'End',
    description: 'Terminal outcome node for process execution',
    icon: <Flag className="w-3.5 h-3.5 text-[#DC2626]" />,
    colorClass: 'border-red-200 dark:border-red-800/80 hover:border-red-400 bg-white dark:bg-slate-800',
    badgeBg: 'bg-red-50 dark:bg-red-950/60 text-[#DC2626] dark:text-rose-400 border border-red-200 dark:border-red-800'
  }
];

export const NodePalette: React.FC = () => {
  const { addNode, loadTemplate, nodes } = useWorkflowStore();
  const { t } = useTheme();

  const onDragStart = (event: React.DragEvent, nodeType: NodeType) => {
    event.dataTransfer.setData('application/reactflow', nodeType);
    event.dataTransfer.effectAllowed = 'move';
  };

  const handleQuickAdd = (type: NodeType) => {
    addNode(type, { x: 350 + Math.random() * 80, y: 200 + Math.random() * 80 });
  };

  return (
    <aside className="w-72 bg-[#F8FAFC] dark:bg-slate-900 border-e border-slate-200 dark:border-slate-800 flex flex-col h-full z-10 select-none shadow-xs text-[#0F172A] dark:text-slate-100 transition-colors">
      {/* Sidebar Header */}
      <div className="p-3.5 border-b border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <FolderKanban className="w-4 h-4 text-[#2563EB] dark:text-blue-400" />
          <h2 className="font-bold text-xs text-[#0F172A] dark:text-slate-100 uppercase tracking-wider">
            {t.toolbox.title}
          </h2>
        </div>
        <span className="text-[11px] bg-slate-100 dark:bg-slate-800 text-[#475569] dark:text-slate-300 px-2 py-0.5 rounded-full font-mono font-bold border border-slate-200 dark:border-slate-700">
          {nodes.length} {t.toolbox.nodesCount}
        </span>
      </div>

      {/* Preset Workflow Templates */}
      <div className="p-2.5 border-b border-slate-200 dark:border-slate-800 bg-slate-100/60 dark:bg-slate-900/60">
        <span className="text-[10px] font-bold uppercase tracking-wider block mb-1.5 px-1 text-[#475569] dark:text-slate-400">
          {t.toolbox.sampleTemplates}
        </span>
        <div className="grid grid-cols-2 gap-1.5">
          <button
            onClick={() => loadTemplate('onboarding')}
            className="px-2 py-1.5 rounded-xl border border-slate-200 dark:border-slate-700 hover:border-blue-400 dark:hover:border-blue-500 bg-white dark:bg-slate-800 text-[11px] font-semibold text-[#0F172A] dark:text-slate-200 text-start truncate transition-colors shadow-2xs cursor-pointer focus:outline-none focus:ring-1 focus:ring-blue-500"
            title="Load Employee Onboarding Workflow"
          >
            {t.toolbox.templates.onboarding}
          </button>
          <button
            onClick={() => loadTemplate('expense')}
            className="px-2 py-1.5 rounded-xl border border-slate-200 dark:border-slate-700 hover:border-blue-400 dark:hover:border-blue-500 bg-white dark:bg-slate-800 text-[11px] font-semibold text-[#0F172A] dark:text-slate-200 text-start truncate transition-colors shadow-2xs cursor-pointer focus:outline-none focus:ring-1 focus:ring-blue-500"
            title="Load Purchase Request Workflow"
          >
            {t.toolbox.templates.expense}
          </button>
          <button
            onClick={() => loadTemplate('itAccess')}
            className="px-2 py-1.5 rounded-xl border border-slate-200 dark:border-slate-700 hover:border-blue-400 dark:hover:border-blue-500 bg-white dark:bg-slate-800 text-[11px] font-semibold text-[#0F172A] dark:text-slate-200 text-start truncate transition-colors shadow-2xs cursor-pointer focus:outline-none focus:ring-1 focus:ring-blue-500"
            title="Load IT Access Request Workflow"
          >
            {t.toolbox.templates.itAccess}
          </button>
          <button
            onClick={() => loadTemplate('empty')}
            className="px-2 py-1.5 rounded-xl border border-slate-200 dark:border-slate-700 hover:border-rose-300 dark:hover:border-rose-500 bg-white dark:bg-slate-800 text-[11px] font-semibold text-[#DC2626] dark:text-rose-400 text-start truncate transition-colors shadow-2xs cursor-pointer focus:outline-none focus:ring-1 focus:ring-rose-500"
            title="Clear canvas"
          >
            {t.toolbox.templates.blank}
          </button>
        </div>
      </div>

      {/* 8. Compact Draggable Node Cards List - Fits 1080p without scrolling */}
      <div className="flex-1 overflow-y-auto p-2.5 space-y-1.5">
        <span className="text-[10px] font-bold uppercase tracking-wider block px-1 text-[#475569] dark:text-slate-400 mb-1">
          {t.toolbox.dragDropComponents}
        </span>

        {PALETTE_ITEMS.map((item) => (
          <div
            key={item.type}
            draggable
            onDragStart={(e) => onDragStart(e, item.type)}
            className={`group p-2 rounded-xl border ${item.colorClass} transition-all duration-150 cursor-grab active:cursor-grabbing shadow-2xs hover:shadow-sm relative overflow-hidden`}
            title={item.description}
          >
            <div className="flex items-center justify-between gap-1.5">
              <div className="flex items-center gap-2 min-w-0 flex-1">
                <div className="p-1 rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 shadow-2xs shrink-0">
                  {item.icon}
                </div>
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-1.5">
                    <h3 className="text-xs font-bold text-[#0F172A] dark:text-slate-100 truncate">
                      {item.label}
                    </h3>
                    <span className={`inline-block text-[9px] font-mono font-bold px-1.5 py-0.2 rounded shrink-0 ${item.badgeBg}`}>
                      {item.category}
                    </span>
                  </div>
                  {/* Single-line description with ellipsis + hover tooltip */}
                  <p 
                    className="text-[11px] text-[#475569] dark:text-slate-300 truncate font-medium mt-0.5 leading-tight" 
                    title={item.description}
                  >
                    {item.description}
                  </p>
                </div>
              </div>

              <div className="flex items-center gap-1 shrink-0">
                <button
                  type="button"
                  onClick={() => handleQuickAdd(item.type)}
                  className="opacity-0 group-hover:opacity-100 focus:opacity-100 p-1 rounded-lg text-[10px] bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 dark:hover:bg-slate-600 text-[#0F172A] dark:text-slate-100 transition-opacity cursor-pointer"
                  title={t.toolbox.quickAdd}
                  aria-label={`${t.toolbox.quickAdd}: ${item.label}`}
                >
                  <Plus className="w-3.5 h-3.5" />
                </button>
                <GripVertical className="w-3.5 h-3.5 text-slate-400 dark:text-slate-500 group-hover:text-slate-600 dark:group-hover:text-slate-300" />
              </div>
            </div>
          </div>
        ))}
      </div>

      <div className="p-2.5 border-t border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-[11px] text-[#475569] dark:text-slate-400 flex items-center justify-between font-medium">
        <span>{t.toolbox.dragHint}</span>
      </div>
    </aside>
  );
};
