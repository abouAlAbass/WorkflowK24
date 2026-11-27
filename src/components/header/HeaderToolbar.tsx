import React, { useState, useRef, useEffect, useCallback } from 'react';
import { 
  Workflow, 
  Play, 
  Square, 
  StepForward, 
  Code2, 
  AlertCircle, 
  RotateCcw,
  Save,
  Upload,
  Database,
  Trash2,
  FileCheck,
  MoreHorizontal,
  CheckCircle2,
  Clock
} from 'lucide-react';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import { useTheme } from '../../hooks/useTheme';

interface HeaderToolbarProps {
  onOpenJsonModal: (initialTab?: 'export' | 'import' | 'database') => void;
  onOpenValidationModal: () => void;
}

export const HeaderToolbar: React.FC<HeaderToolbarProps> = ({ 
  onOpenJsonModal, 
  onOpenValidationModal 
}) => {
  const { 
    workflowName, 
    setWorkflowName, 
    isSimulating, 
    startSimulation, 
    stopSimulation, 
    stepSimulation,
    validateWorkflow,
    resetCanvas,
    savedWorkflows,
    saveWorkflowToDb,
    loadWorkflowFromDb,
    deleteWorkflowFromDb,
    appMode,
    openModeModal
  } = useWorkflowStore();

  const { t } = useTheme();

  const [savedSuccessMsg, setSavedSuccessMsg] = useState(false);
  const [isOverflowOpen, setIsOverflowOpen] = useState(false);
  const [isSavedDbSubmenuOpen, setIsSavedDbSubmenuOpen] = useState(false);
  const [lastSavedTimestamp, setLastSavedTimestamp] = useState<string>(() => new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }));
  const [isDirty, setIsDirty] = useState(false);

  const overflowMenuRef = useRef<HTMLDivElement>(null);
  const overflowButtonRef = useRef<HTMLButtonElement>(null);

  const validationIssues = validateWorkflow();
  const errorCount = validationIssues.filter(i => i.type === 'error').length;
  const warningCount = validationIssues.filter(i => i.type === 'warning').length;

  // Mark draft as auto-saved when workflowName changes
  const handleWorkflowNameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setWorkflowName(e.target.value);
    setIsDirty(true);
    setLastSavedTimestamp(new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }));
  };

  const handleQuickSave = () => {
    saveWorkflowToDb();
    setSavedSuccessMsg(true);
    setIsDirty(false);
    setLastSavedTimestamp(new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }));
    setTimeout(() => setSavedSuccessMsg(false), 2000);
  };

  // Close overflow menu on outside click
  useEffect(() => {
    const handleOutsideClick = (event: MouseEvent) => {
      if (
        overflowMenuRef.current && 
        !overflowMenuRef.current.contains(event.target as Node) &&
        overflowButtonRef.current &&
        !overflowButtonRef.current.contains(event.target as Node)
      ) {
        setIsOverflowOpen(false);
        setIsSavedDbSubmenuOpen(false);
      }
    };

    if (isOverflowOpen) {
      document.addEventListener('mousedown', handleOutsideClick);
    }
    return () => {
      document.removeEventListener('mousedown', handleOutsideClick);
    };
  }, [isOverflowOpen]);

  // Keyboard navigation for overflow menu (Esc to close)
  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === 'Escape') {
      setIsOverflowOpen(false);
      setIsSavedDbSubmenuOpen(false);
      overflowButtonRef.current?.focus();
    }
  }, []);

  return (
    <header className="h-16 bg-white dark:bg-slate-900 border-b border-slate-200 dark:border-slate-800 flex items-center justify-between px-4 z-20 select-none shadow-xs text-[#0F172A] dark:text-slate-100 transition-colors">
      {/* 2. Brand & Workflow Title - Fixed truncation & repositioned studio badge */}
      <div className="flex items-center gap-3 min-w-0 flex-1 max-w-xl mr-4">
        <div 
          className="p-2 rounded-2xl bg-gradient-to-tr from-[#2563EB] to-blue-500 text-white shadow-md shadow-blue-500/20 shrink-0"
          aria-hidden="true"
        >
          <Workflow className="w-6 h-6" />
        </div>

        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <input
              type="text"
              value={workflowName}
              onChange={handleWorkflowNameChange}
              title={workflowName}
              aria-label="Workflow Title"
              className="ty-workflow-title bg-transparent truncate focus:truncate-none focus:px-2 focus:py-0.5 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#2563EB] transition-all border-b border-transparent hover:border-slate-300 dark:hover:border-slate-700 min-w-0 max-w-full"
            />
          </div>
          <div className="flex items-center gap-2 -mt-0.5">
            <span className="text-[10px] font-mono font-bold px-2 py-0.5 rounded-md bg-blue-50 dark:bg-blue-950/60 text-[#2563EB] dark:text-blue-400 border border-blue-200 dark:border-blue-800 shrink-0">
              Tamam Workflow Studio
            </span>
            <span className="text-[11px] text-[#475569] dark:text-slate-400 truncate font-medium">
              Visual Workflow Designer
            </span>
          </div>
        </div>
      </div>

      {/* Center Actions / Simulator Controls */}
      <div className="flex items-center gap-2 shrink-0">
        {isSimulating ? (
          <div className="flex items-center gap-2 p-1.5 rounded-2xl bg-amber-50 dark:bg-amber-950/40 border border-amber-300 dark:border-amber-700 shadow-2xs">
            <span className="text-xs font-bold text-[#D97706] dark:text-amber-400 px-2 flex items-center gap-1.5">
              <span className="w-2 h-2 rounded-full bg-amber-500 animate-ping" />
              {t.toolbar.simulating}
            </span>
            <button
              onClick={stepSimulation}
              className="px-3.5 py-1.5 bg-[#D97706] hover:bg-amber-700 text-white text-xs font-bold rounded-xl flex items-center gap-1.5 shadow-xs transition-colors focus:outline-none focus:ring-2 focus:ring-amber-500 cursor-pointer"
            >
              <StepForward className="w-3.5 h-3.5" />
              <span>{t.toolbar.nextStep}</span>
            </button>
            <button
              onClick={stopSimulation}
              className="px-2.5 py-1.5 bg-white dark:bg-slate-800 hover:bg-rose-50 dark:hover:bg-rose-950/50 text-[#475569] dark:text-slate-200 hover:text-[#DC2626] text-xs font-bold rounded-xl flex items-center gap-1 border border-slate-300 dark:border-slate-700 shadow-2xs transition-colors focus:outline-none focus:ring-2 focus:ring-rose-500 cursor-pointer"
            >
              <Square className="w-3.5 h-3.5 text-[#DC2626]" />
              <span>{t.toolbar.stopSimulation}</span>
            </button>
          </div>
        ) : (
          /* Primary Visible Button 1: Test Simulation */
          <button
            onClick={startSimulation}
            className="px-4 py-2 bg-gradient-to-r from-[#059669] to-teal-600 hover:from-emerald-700 hover:to-teal-700 text-white rounded-xl text-xs font-bold flex items-center gap-1.5 shadow-sm transition-all focus:outline-none focus:ring-2 focus:ring-emerald-500 cursor-pointer"
          >
            <Play className="w-4 h-4 fill-white" />
            <span>{t.toolbar.testSimulation}</span>
          </button>
        )}

        {/* Primary Visible Button 2: Validate */}
        <button
          onClick={onOpenValidationModal}
          aria-label={`Validate workflow. ${errorCount} errors, ${warningCount} warnings.`}
          className={`px-3 py-2 rounded-xl text-xs font-bold flex items-center gap-1.5 border transition-all shadow-2xs focus:outline-none focus:ring-2 focus:ring-blue-500 cursor-pointer ${
            errorCount > 0
              ? 'bg-rose-50 dark:bg-rose-950/50 text-[#DC2626] dark:text-rose-400 border-rose-200 dark:border-rose-800'
              : warningCount > 0
                ? 'bg-amber-50 dark:bg-amber-950/50 text-[#D97706] dark:text-amber-400 border-amber-200 dark:border-amber-800'
                : 'bg-white dark:bg-slate-800 text-[#0F172A] dark:text-slate-100 border-slate-300 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700'
          }`}
        >
          <AlertCircle className={`w-4 h-4 ${errorCount > 0 ? 'text-[#DC2626]' : warningCount > 0 ? 'text-[#D97706]' : 'text-[#059669]'}`} />
          <span>{t.toolbar.validate}</span>
          {(errorCount > 0 || warningCount > 0) && (
            <span className={`text-[10px] px-1.5 py-0.2 rounded-full font-mono font-extrabold ${errorCount > 0 ? 'bg-[#DC2626] text-white' : 'bg-[#D97706] text-white'}`}>
              {errorCount + warningCount}
            </span>
          )}
        </button>

        {/* Primary Visible Button 3: Save Definition (Named Database Save) */}
        <button
          onClick={handleQuickSave}
          className={`px-3.5 py-2 rounded-xl text-xs font-bold flex items-center gap-1.5 border shadow-2xs transition-all focus:outline-none focus:ring-2 focus:ring-blue-500 cursor-pointer ${
            savedSuccessMsg 
              ? 'bg-[#059669] text-white border-[#059669]' 
              : 'bg-[#EFF6FF] dark:bg-blue-950/50 text-[#2563EB] dark:text-blue-400 border-blue-200 dark:border-blue-800 hover:bg-blue-100 dark:hover:bg-blue-900/60'
          }`}
          title="Save workflow definition to database as a named version"
        >
          {savedSuccessMsg ? <FileCheck className="w-4 h-4" /> : <Save className="w-4 h-4" />}
          <span>{savedSuccessMsg ? t.toolbar.savedToDb : t.toolbar.saveDefinition}</span>
        </button>

        {/* 6. Clarify auto-save status indicator in toolbar */}
        <div 
          className="hidden xl:flex items-center gap-1.5 px-2.5 py-1.5 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200 dark:border-slate-700 text-[11px] font-medium text-[#475569] dark:text-slate-300"
          title={`Draft state auto-saved at ${lastSavedTimestamp}`}
        >
          {isDirty ? (
            <>
              <Clock className="w-3.5 h-3.5 text-amber-500" />
              <span>{t.toolbar.unsavedChanges}</span>
            </>
          ) : (
            <>
              <CheckCircle2 className="w-3.5 h-3.5 text-[#059669] dark:text-emerald-400" />
              <span>{t.toolbar.allChangesSaved}</span>
            </>
          )}
        </div>
      </div>

      {/* Right Side Tools & Overflow Menu */}
      <div className="flex items-center gap-2 shrink-0">
        {/* Runtime Mode Selector - Styled as a badge/chip status indicator */}
        <button
          onClick={openModeModal}
          className={`py-1.5 px-3 rounded-full border flex items-center gap-2 cursor-pointer text-xs font-semibold transition-all shadow-2xs focus:outline-none focus:ring-2 focus:ring-indigo-500 ${
            appMode === 'connected'
              ? 'bg-indigo-50 dark:bg-indigo-950/60 border-indigo-300 dark:border-indigo-800 text-indigo-700 dark:text-indigo-400 hover:bg-indigo-100 dark:hover:bg-indigo-900/60'
              : 'bg-emerald-50 dark:bg-emerald-950/60 border-emerald-300 dark:border-emerald-800 text-emerald-700 dark:text-emerald-400 hover:bg-emerald-100 dark:hover:bg-emerald-900/60'
          }`}
          title={t.toolbar.runtimeSelector}
          aria-label={`Current Mode: ${appMode === 'connected' ? 'Connected API' : 'Demo Mode'}. Click to configure.`}
        >
          <span className={`w-2 h-2 rounded-full ${appMode === 'connected' ? 'bg-indigo-500 animate-pulse' : 'bg-emerald-500'}`} />
          <span>{appMode === 'connected' ? t.toolbar.connectedMode : t.toolbar.demoMode}</span>
        </button>

        {/* 1. Single Overflow Menu ("More" with ⋯ icon) containing Export, Import, Saved DB, and Reset */}
        <div className="relative" onKeyDown={handleKeyDown}>
          <button
            ref={overflowButtonRef}
            onClick={() => setIsOverflowOpen(!isOverflowOpen)}
            aria-haspopup="true"
            aria-expanded={isOverflowOpen}
            aria-label={t.toolbar.moreActions}
            className="p-2 rounded-xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 hover:bg-slate-50 dark:hover:bg-slate-700 text-[#0F172A] dark:text-slate-100 shadow-2xs transition-colors flex items-center gap-1.5 px-2.5 font-bold text-xs focus:outline-none focus:ring-2 focus:ring-blue-500 cursor-pointer"
            title={t.toolbar.moreActions}
          >
            <MoreHorizontal className="w-4 h-4" />
            <span className="hidden sm:inline">{t.toolbar.moreActions}</span>
            {savedWorkflows.length > 0 && (
              <span className="text-[10px] bg-purple-100 dark:bg-purple-950/80 text-[#7C3AED] dark:text-purple-300 px-1.5 py-0.2 rounded-full font-mono font-bold">
                {savedWorkflows.length}
              </span>
            )}
          </button>

          {isOverflowOpen && (
            <div
              ref={overflowMenuRef}
              role="menu"
              aria-orientation="vertical"
              className="absolute right-0 top-full mt-1.5 w-64 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-2xl shadow-2xl p-1.5 z-50 animate-in fade-in zoom-in-95 text-[#0F172A] dark:text-slate-100"
            >
              {/* Menu Item 1: Export JSON */}
              <button
                role="menuitem"
                onClick={() => {
                  setIsOverflowOpen(false);
                  onOpenJsonModal('export');
                }}
                className="w-full text-left px-3 py-2 text-xs font-semibold rounded-xl flex items-center gap-2.5 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors focus:outline-none focus:bg-slate-100 dark:focus:bg-slate-800 cursor-pointer"
                title={t.toolbar.exportJsonDesc}
              >
                <Code2 className="w-4 h-4 text-[#2563EB] dark:text-blue-400 shrink-0" />
                <div className="flex-1">
                  <span>{t.toolbar.exportJson}</span>
                  <span className="text-[10px] text-[#475569] dark:text-slate-400 block font-normal">
                    {t.toolbar.exportJsonDesc}
                  </span>
                </div>
              </button>

              {/* Menu Item 2: Import JSON */}
              <button
                role="menuitem"
                onClick={() => {
                  setIsOverflowOpen(false);
                  onOpenJsonModal('import');
                }}
                className="w-full text-left px-3 py-2 text-xs font-semibold rounded-xl flex items-center gap-2.5 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors focus:outline-none focus:bg-slate-100 dark:focus:bg-slate-800 cursor-pointer"
                title={t.toolbar.importJsonDesc}
              >
                <Upload className="w-4 h-4 text-[#2563EB] dark:text-blue-400 shrink-0" />
                <div className="flex-1">
                  <span>{t.toolbar.importJson}</span>
                  <span className="text-[10px] text-[#475569] dark:text-slate-400 block font-normal">
                    {t.toolbar.importJsonDesc}
                  </span>
                </div>
              </button>

              <div className="my-1 border-t border-slate-100 dark:border-slate-800" />

              {/* Menu Item 3: Saved DB with count and expand preview */}
              <div className="relative">
                <button
                  role="menuitem"
                  onClick={() => setIsSavedDbSubmenuOpen(!isSavedDbSubmenuOpen)}
                  className="w-full text-left px-3 py-2 text-xs font-semibold rounded-xl flex items-center justify-between hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors focus:outline-none focus:bg-slate-100 dark:focus:bg-slate-800 cursor-pointer"
                  title={t.toolbar.savedDbDesc}
                >
                  <div className="flex items-center gap-2.5">
                    <Database className="w-4 h-4 text-[#7C3AED] dark:text-purple-400 shrink-0" />
                    <div>
                      <span>{t.toolbar.savedDb}</span>
                      <span className="text-[10px] text-[#475569] dark:text-slate-400 block font-normal">
                        {savedWorkflows.length} version{savedWorkflows.length === 1 ? '' : 's'} stored
                      </span>
                    </div>
                  </div>
                  <span className="text-[10px] bg-purple-100 dark:bg-purple-950/80 text-[#7C3AED] dark:text-purple-300 px-2 py-0.5 rounded-full font-mono font-bold">
                    {savedWorkflows.length}
                  </span>
                </button>

                {isSavedDbSubmenuOpen && (
                  <div className="mt-1 p-2 bg-slate-50 dark:bg-slate-850 rounded-xl border border-slate-200 dark:border-slate-700">
                    <div className="flex items-center justify-between pb-1.5 mb-1.5 border-b border-slate-200 dark:border-slate-700 text-[10px] font-bold text-[#475569] dark:text-slate-400 uppercase tracking-wider">
                      <span>Saved Records</span>
                      <button
                        onClick={() => {
                          setIsOverflowOpen(false);
                          onOpenJsonModal('database');
                        }}
                        className="text-[#2563EB] dark:text-blue-400 hover:underline cursor-pointer"
                      >
                        Manage DB ↗
                      </button>
                    </div>

                    {savedWorkflows.length === 0 ? (
                      <p className="text-xs text-[#475569] dark:text-slate-400 py-1 italic text-center font-medium">
                        No saved definitions in database.
                      </p>
                    ) : (
                      <div className="max-h-36 overflow-y-auto space-y-1 pr-1">
                        {savedWorkflows.map((rec) => (
                          <div
                            key={rec.id}
                            className="flex items-center justify-between p-1.5 rounded-lg bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 text-xs transition-colors hover:border-blue-300 dark:hover:border-blue-700"
                          >
                            <button
                              onClick={() => {
                                loadWorkflowFromDb(rec.id);
                                setIsOverflowOpen(false);
                              }}
                              className="flex-1 text-left truncate font-bold text-[#0F172A] dark:text-slate-100 pr-1.5 cursor-pointer"
                              title={`Load "${rec.name}"`}
                            >
                              <span className="block truncate">{rec.name}</span>
                              <span className="text-[10px] text-[#475569] dark:text-slate-400 block font-mono font-medium">{rec.savedAt}</span>
                            </button>
                            <button
                              onClick={() => deleteWorkflowFromDb(rec.id)}
                              className="p-1 text-slate-400 hover:text-[#DC2626] dark:hover:text-rose-400 transition-colors cursor-pointer"
                              title="Delete definition"
                              aria-label={`Delete saved definition ${rec.name}`}
                            >
                              <Trash2 className="w-3.5 h-3.5" />
                            </button>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )}
              </div>

              <div className="my-1 border-t border-slate-100 dark:border-slate-800" />

              {/* Menu Item 4: Reset Canvas */}
              <button
                role="menuitem"
                onClick={() => {
                  setIsOverflowOpen(false);
                  resetCanvas();
                }}
                className="w-full text-left px-3 py-2 text-xs font-semibold rounded-xl flex items-center gap-2.5 text-[#DC2626] dark:text-rose-400 hover:bg-rose-50 dark:hover:bg-rose-950/40 transition-colors focus:outline-none focus:bg-rose-50 dark:focus:bg-rose-950/40 cursor-pointer"
                title={t.toolbar.resetCanvasDesc}
              >
                <RotateCcw className="w-4 h-4 shrink-0" />
                <div className="flex-1">
                  <span>{t.toolbar.resetCanvas}</span>
                  <span className="text-[10px] text-rose-500/80 dark:text-rose-400/80 block font-normal">
                    {t.toolbar.resetCanvasDesc}
                  </span>
                </div>
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  );
};
