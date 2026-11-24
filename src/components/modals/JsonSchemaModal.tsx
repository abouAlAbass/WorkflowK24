import React, { useState } from 'react';
import { X, Copy, Check, Download, Upload, Database, FileCode, HardDrive } from 'lucide-react';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';

interface JsonSchemaModalProps {
  isOpen: boolean;
  onClose: () => void;
  initialTab?: 'export' | 'import' | 'database';
}

export const JsonSchemaModal: React.FC<JsonSchemaModalProps> = ({ 
  isOpen, 
  onClose, 
  initialTab = 'export' 
}) => {
  const { 
    exportWorkflowJson, 
    importWorkflowJson, 
    saveWorkflowToDb, 
    savedWorkflows, 
    loadWorkflowFromDb,
    deleteWorkflowFromDb
  } = useWorkflowStore();

  const [copied, setCopied] = useState(false);
  const [importText, setImportText] = useState('');
  const [activeTab, setActiveTab] = useState<'export' | 'import' | 'database'>(initialTab);
  const [importError, setImportError] = useState<string | null>(null);
  const [customDbName, setCustomDbName] = useState('');

  if (!isOpen) return null;

  const currentSchema = exportWorkflowJson();
  const jsonString = JSON.stringify(currentSchema, null, 2);

  const handleCopy = () => {
    navigator.clipboard.writeText(jsonString);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleDownload = () => {
    const blob = new Blob([jsonString], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${currentSchema.name.toLowerCase().replace(/\s+/g, '-')}-definition.json`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (event) => {
      try {
        const text = event.target?.result as string;
        setImportText(text);
        const parsed = JSON.parse(text);
        if (!parsed.nodes || !Array.isArray(parsed.nodes)) {
          throw new Error('Invalid JSON: Missing "nodes" array.');
        }
        importWorkflowJson(parsed);
        onClose();
      } catch (err: any) {
        setImportError(err.message || 'Invalid JSON file.');
      }
    };
    reader.readAsText(file);
  };

  const handleImportSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setImportError(null);
    try {
      const parsed = JSON.parse(importText);
      if (!parsed.nodes || !Array.isArray(parsed.nodes)) {
        throw new Error('Invalid schema: Missing "nodes" array.');
      }
      importWorkflowJson(parsed);
      onClose();
    } catch (err: any) {
      setImportError(err.message || 'Invalid JSON syntax.');
    }
  };

  const handleSaveToDb = (e: React.FormEvent) => {
    e.preventDefault();
    saveWorkflowToDb(customDbName.trim() || undefined);
    setCustomDbName('');
    setActiveTab('database');
  };

  return (
    <div className="fixed inset-0 z-50 bg-slate-950/60 backdrop-blur-xs flex items-center justify-center p-4 select-none animate-fade-in text-[#0F172A] dark:text-slate-100">
      <div className="border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 rounded-3xl w-full max-w-3xl shadow-2xl overflow-hidden flex flex-col max-h-[85vh] transition-colors">
        {/* Modal Header */}
        <div className="p-4 border-b border-slate-200 dark:border-slate-800 bg-[#F8FAFC] dark:bg-slate-900 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <div className="flex items-center gap-1 p-1 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl shadow-2xs">
              <button
                onClick={() => setActiveTab('export')}
                className={`px-3 py-1.5 rounded-lg text-xs font-bold flex items-center gap-1.5 transition-colors ${
                  activeTab === 'export'
                    ? 'bg-[#2563EB] text-white shadow-xs'
                    : 'text-[#475569] dark:text-slate-300 hover:text-[#0F172A] dark:hover:text-slate-100'
                }`}
              >
                <Download className="w-3.5 h-3.5" />
                <span>Export JSON File</span>
              </button>
              <button
                onClick={() => setActiveTab('import')}
                className={`px-3 py-1.5 rounded-lg text-xs font-bold flex items-center gap-1.5 transition-colors ${
                  activeTab === 'import'
                    ? 'bg-[#2563EB] text-white shadow-xs'
                    : 'text-[#475569] dark:text-slate-300 hover:text-[#0F172A] dark:hover:text-slate-100'
                }`}
              >
                <Upload className="w-3.5 h-3.5" />
                <span>Import JSON File</span>
              </button>
              <button
                onClick={() => setActiveTab('database')}
                className={`px-3 py-1.5 rounded-lg text-xs font-bold flex items-center gap-1.5 transition-colors ${
                  activeTab === 'database'
                    ? 'bg-[#2563EB] text-white shadow-xs'
                    : 'text-[#475569] dark:text-slate-300 hover:text-[#0F172A] dark:hover:text-slate-100'
                }`}
              >
                <Database className="w-3.5 h-3.5" />
                <span>Mock DB Persistence</span>
              </button>
            </div>
          </div>

          <button
            onClick={onClose}
            aria-label="Close JSON schema modal"
            className="p-1.5 rounded-lg text-slate-400 hover:text-[#0F172A] dark:hover:text-slate-100 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Tab 1: Export JSON File */}
        {activeTab === 'export' && (
          <div className="flex-1 flex flex-col p-5 overflow-hidden">
            <div className="flex items-center justify-between mb-3">
              <div>
                <h3 className="text-xs font-extrabold text-[#0F172A]">
                  Tamam Workflow Schema (JSON Definition)
                </h3>
                <p className="text-[11px] text-[#475569] font-medium">
                  Export or copy full graph state containing nodes, edges, position coordinates, and data payloads.
                </p>
              </div>

              <div className="flex items-center gap-2">
                <button
                  onClick={handleCopy}
                  className="px-3 py-1.5 rounded-xl text-xs font-bold flex items-center gap-1.5 border border-slate-300 bg-white hover:bg-slate-50 text-[#0F172A] shadow-2xs transition-colors"
                >
                  {copied ? <Check className="w-3.5 h-3.5 text-[#059669]" /> : <Copy className="w-3.5 h-3.5" />}
                  <span>{copied ? 'Copied!' : 'Copy JSON'}</span>
                </button>

                <button
                  onClick={handleDownload}
                  className="px-3.5 py-1.5 bg-[#2563EB] hover:bg-blue-700 text-white rounded-xl text-xs font-bold flex items-center gap-1.5 shadow-sm"
                >
                  <Download className="w-3.5 h-3.5" />
                  <span>Download .json File</span>
                </button>
              </div>
            </div>

            <pre className="flex-1 overflow-auto p-4 rounded-2xl border border-slate-800 bg-[#0F172A] text-indigo-300 text-xs font-mono leading-relaxed shadow-inner">
              {jsonString}
            </pre>
          </div>
        )}

        {/* Tab 2: Import JSON File */}
        {activeTab === 'import' && (
          <form onSubmit={handleImportSubmit} className="flex-1 flex flex-col p-5">
            <div className="mb-3">
              <h3 className="text-xs font-extrabold text-[#0F172A] mb-1">
                Import Workflow Definition File (.json)
              </h3>
              <p className="text-[11px] text-[#475569] font-medium">
                Upload a `.json` definition file or paste valid schema JSON below to restore node graph onto canvas.
              </p>
            </div>

            {/* File Upload Drop Zone */}
            <div className="mb-3 p-4 rounded-2xl border-2 border-dashed border-blue-200 bg-[#EFF6FF]/40 flex items-center justify-between hover:border-[#2563EB] transition-colors">
              <div className="flex items-center gap-3">
                <FileCode className="w-6 h-6 text-[#2563EB]" />
                <div>
                  <span className="text-xs font-bold text-[#0F172A] block">
                    Choose a JSON workflow definition file
                  </span>
                  <span className="text-[10px] text-[#475569] font-medium">
                    Supports Tamam Workflow JSON schema files (*.json)
                  </span>
                </div>
              </div>

              <label className="px-3.5 py-1.5 bg-[#2563EB] hover:bg-blue-700 text-white rounded-xl text-xs font-bold cursor-pointer shadow-2xs">
                <span>Browse File</span>
                <input
                  type="file"
                  accept=".json,application/json"
                  onChange={handleFileUpload}
                  className="hidden"
                />
              </label>
            </div>

            <textarea
              rows={8}
              value={importText}
              onChange={(e) => setImportText(e.target.value)}
              placeholder='Or paste raw JSON schema here...\n{\n  "name": "Custom Workflow",\n  "nodes": [...],\n  "edges": [...]\n}'
              className="w-full flex-1 p-3.5 rounded-2xl border border-slate-300 bg-white text-[#0F172A] font-mono text-xs focus:outline-none focus:ring-2 focus:ring-[#3B82F6] focus:border-[#3B82F6]"
            />

            {importError && (
              <p className="text-xs text-[#DC2626] my-2 p-2.5 bg-rose-50 rounded-xl border border-red-200 font-mono font-bold">
                Error: {importError}
              </p>
            )}

            <div className="flex items-center justify-end gap-2 mt-4">
              <button
                type="button"
                onClick={onClose}
                className="px-4 py-2 bg-slate-100 text-[#0F172A] hover:bg-slate-200 rounded-xl text-xs font-bold"
              >
                Cancel
              </button>
              <button
                type="submit"
                className="px-4 py-2 bg-[#2563EB] hover:bg-blue-700 text-white rounded-xl text-xs font-bold flex items-center gap-1.5 shadow-sm"
              >
                <Upload className="w-4 h-4" />
                <span>Load Schema onto Canvas</span>
              </button>
            </div>
          </form>
        )}

        {/* Tab 3: Mock DB Persistence Manager */}
        {activeTab === 'database' && (
          <div className="flex-1 flex flex-col p-5 overflow-hidden">
            <div className="mb-4">
              <h3 className="text-xs font-extrabold text-[#0F172A]">
                Database Workflow Storage (Mock Database)
              </h3>
              <p className="text-[11px] text-[#475569] font-medium">
                Store definitions directly into LocalStorage to emulate database persistence before API backend integration.
              </p>
            </div>

            {/* Save Form */}
            <form onSubmit={handleSaveToDb} className="flex gap-2 mb-4">
              <input
                type="text"
                placeholder="Workflow name (e.g. Purchase Order Approval V2)"
                value={customDbName}
                onChange={(e) => setCustomDbName(e.target.value)}
                className="flex-1 px-3.5 py-2.5 rounded-xl text-xs bg-white border border-slate-300 text-[#0F172A] font-bold focus:outline-none focus:ring-2 focus:ring-[#3B82F6]"
              />
              <button
                type="submit"
                className="px-4 py-2 bg-[#2563EB] hover:bg-blue-700 text-white rounded-xl text-xs font-bold flex items-center gap-1.5 shadow-sm shrink-0"
              >
                <HardDrive className="w-4 h-4" />
                <span>Save to Database</span>
              </button>
            </form>

            {/* List of Saved Workflows */}
            <div className="flex-1 overflow-y-auto space-y-2 pr-1">
              {savedWorkflows.length === 0 ? (
                <div className="p-8 text-center rounded-2xl border border-dashed border-slate-300 bg-[#F8FAFC] text-[#475569]">
                  <Database className="w-8 h-8 text-[#2563EB] mx-auto mb-2 opacity-60" />
                  <p className="text-xs font-bold text-[#0F172A]">No saved workflow definitions in database yet.</p>
                  <p className="text-[11px] text-[#475569] mt-1 font-medium">Use the form above to save your first workflow version.</p>
                </div>
              ) : (
                savedWorkflows.map((rec) => (
                  <div
                    key={rec.id}
                    className="flex items-center justify-between p-3 rounded-2xl border border-slate-200 bg-white hover:bg-blue-50/50 text-xs transition-colors shadow-2xs"
                  >
                    <div>
                      <h4 className="font-bold text-[#0F172A] text-sm">
                        {rec.name}
                      </h4>
                      <span className="text-[10px] text-[#475569] font-mono font-medium">
                        Saved At: {rec.savedAt} • ID: {rec.id}
                      </span>
                    </div>

                    <div className="flex items-center gap-2">
                      <button
                        onClick={() => {
                          loadWorkflowFromDb(rec.id);
                          onClose();
                        }}
                        className="px-3 py-1.5 bg-[#2563EB] hover:bg-blue-700 text-white rounded-xl text-xs font-bold shadow-2xs"
                      >
                        Load to Canvas
                      </button>

                      <button
                        onClick={() => deleteWorkflowFromDb(rec.id)}
                        className="p-1.5 text-slate-400 hover:text-[#DC2626] transition-colors"
                        title="Delete saved workflow"
                      >
                        <X className="w-4 h-4" />
                      </button>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
