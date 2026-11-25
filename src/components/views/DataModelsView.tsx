import React, { useState, useMemo } from 'react';
import { 
  Database, 
  Plus, 
  Table, 
  Layers, 
  Key, 
  Check, 
  Server, 
  Trash2, 
  Edit2, 
  RefreshCw, 
  Search, 
  X, 
  AlertCircle, 
  Sparkles,
  FileText
} from 'lucide-react';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import { useSmartFormStore } from '../../store/useSmartFormStore';
import type { SmartObjectField, SmartObjectCategory, SmartObjectRecord } from '../../types/workflow';

export const DataModelsView: React.FC = () => {
  const { 
    smartObjects, 
    smartObjectData,
    createSmartObject, 
    updateSmartObject, 
    deleteSmartObject,
    addSmartObjectField,
    deleteSmartObjectField,
    fetchSmartObjectRecords,
    addSmartObjectRecord,
    updateSmartObjectRecord,
    deleteSmartObjectRecord,
    setCurrentTab
  } = useWorkflowStore();
  const generateFromSmartObject = useSmartFormStore((s) => s.generateFromSmartObject);

  const [selectedSOId, setSelectedSOId] = useState<string>(smartObjects[0]?.id || '');
  const [activeTab, setActiveTab] = useState<'schema' | 'records'>('schema');
  const [searchTerm, setSearchTerm] = useState('');
  const [isFetchingRecords, setIsFetchingRecords] = useState(false);

  // Modals state
  const [isCreateEntityOpen, setIsCreateEntityOpen] = useState(false);
  const [isEditEntityOpen, setIsEditEntityOpen] = useState(false);
  const [isDeleteEntityConfirmOpen, setIsDeleteEntityConfirmOpen] = useState(false);
  const [isAddFieldOpen, setIsAddFieldOpen] = useState(false);

  // Record modal state
  const [recordModalMode, setRecordModalMode] = useState<'add' | 'edit' | null>(null);
  const [editingRecord, setEditingRecord] = useState<SmartObjectRecord | null>(null);
  const [recordFormData, setRecordFormData] = useState<Record<string, any>>({});
  const [deleteRecordTarget, setDeleteRecordTarget] = useState<SmartObjectRecord | null>(null);

  // Create Entity Form
  const [newEntityName, setNewEntityName] = useState('');
  const [newEntityDisplayName, setNewEntityDisplayName] = useState('');
  const [newEntityDescription, setNewEntityDescription] = useState('');
  const [newEntityCategory, setNewEntityCategory] = useState<SmartObjectCategory>('Core');
  const [newEntityPkName, setNewEntityPkName] = useState('Id');
  const [newEntityPkType, setNewEntityPkType] = useState<'GUID' | 'Number' | 'String'>('GUID');

  // Edit Entity Form
  const [editDisplayName, setEditDisplayName] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [editCategory, setEditCategory] = useState<SmartObjectCategory>('Core');

  // Add Field Form
  const [newFieldName, setNewFieldName] = useState('');
  const [newFieldType, setNewFieldType] = useState<'String' | 'Number' | 'Boolean' | 'DateTime' | 'GUID'>('String');
  const [newFieldIsPk, setNewFieldIsPk] = useState(false);
  const [newFieldIsRequired, setNewFieldIsRequired] = useState(false);

  // Resolve currently active SmartObject (fallback to first available)
  const selectedSO = smartObjects.find(s => s.id === selectedSOId) || smartObjects[0];
  const primaryKeyField = selectedSO?.fields.find(f => f.isPrimaryKey)?.name || selectedSO?.fields[0]?.name || 'id';

  // Active records
  const currentRecords = useMemo(() => {
    if (!selectedSO) return [];
    return smartObjectData[selectedSO.id] || [];
  }, [selectedSO, smartObjectData]);

  // Filtered records for search
  const filteredRecords = useMemo(() => {
    if (!searchTerm.trim()) return currentRecords;
    const term = searchTerm.toLowerCase();
    return currentRecords.filter(r =>
      Object.values(r).some(val => String(val ?? '').toLowerCase().includes(term))
    );
  }, [currentRecords, searchTerm]);

  // Helper to trigger records fetch
  const handleRefreshRecords = async () => {
    if (!selectedSO) return;
    setIsFetchingRecords(true);
    try {
      await fetchSmartObjectRecords(selectedSO.id);
    } finally {
      setTimeout(() => setIsFetchingRecords(false), 300);
    }
  };

  // Open Create Entity Modal
  const openCreateEntity = () => {
    setNewEntityName('');
    setNewEntityDisplayName('');
    setNewEntityDescription('');
    setNewEntityCategory('Core');
    setNewEntityPkName('Id');
    setNewEntityPkType('GUID');
    setIsCreateEntityOpen(true);
  };

  const handleCreateEntitySubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newEntityName.trim()) return;

    const created = await createSmartObject({
      name: newEntityName.trim(),
      displayName: newEntityDisplayName.trim() || newEntityName.trim(),
      description: newEntityDescription.trim(),
      category: newEntityCategory,
      fields: [
        { name: newEntityPkName.trim() || 'Id', type: newEntityPkType, isPrimaryKey: true, isRequired: true }
      ]
    });

    setSelectedSOId(created.id);
    setIsCreateEntityOpen(false);
  };

  // Open Edit Entity Modal
  const openEditEntity = () => {
    if (!selectedSO) return;
    setEditDisplayName(selectedSO.displayName);
    setEditDescription(selectedSO.description);
    setEditCategory(selectedSO.category);
    setIsEditEntityOpen(true);
  };

  const handleEditEntitySubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedSO) return;

    await updateSmartObject(selectedSO.id, {
      displayName: editDisplayName.trim() || selectedSO.displayName,
      description: editDescription.trim(),
      category: editCategory
    });

    setIsEditEntityOpen(false);
  };

  // Handle Delete Entity
  const handleDeleteEntitySubmit = async () => {
    if (!selectedSO) return;
    const targetId = selectedSO.id;
    await deleteSmartObject(targetId);
    const remaining = smartObjects.filter(s => s.id !== targetId);
    if (remaining.length > 0) {
      setSelectedSOId(remaining[0].id);
    }
    setIsDeleteEntityConfirmOpen(false);
  };

  // Add Field Submit
  const handleAddFieldSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedSO || !newFieldName.trim()) return;

    const field: SmartObjectField = {
      name: newFieldName.trim(),
      type: newFieldType,
      isPrimaryKey: newFieldIsPk,
      isRequired: newFieldIsRequired
    };

    addSmartObjectField(selectedSO.id, field);
    setNewFieldName('');
    setNewFieldType('String');
    setNewFieldIsPk(false);
    setNewFieldIsRequired(false);
    setIsAddFieldOpen(false);
  };

  // Record CRUD Handlers
  const openAddRecordModal = () => {
    if (!selectedSO) return;
    const initial: Record<string, any> = {};
    selectedSO.fields.forEach(f => {
      if (f.type === 'GUID') {
        initial[f.name] = `id-${Math.random().toString(36).substring(2, 9)}`;
      } else if (f.type === 'Number') {
        initial[f.name] = 0;
      } else if (f.type === 'Boolean') {
        initial[f.name] = false;
      } else if (f.type === 'DateTime') {
        initial[f.name] = new Date().toISOString().split('T')[0];
      } else {
        initial[f.name] = '';
      }
    });
    setRecordFormData(initial);
    setEditingRecord(null);
    setRecordModalMode('add');
  };

  const openEditRecordModal = (record: SmartObjectRecord) => {
    setEditingRecord(record);
    setRecordFormData({ ...record });
    setRecordModalMode('edit');
  };

  const handleRecordFormSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedSO) return;

    if (recordModalMode === 'add') {
      await addSmartObjectRecord(selectedSO.id, recordFormData);
    } else if (recordModalMode === 'edit' && editingRecord) {
      const pkVal = editingRecord[primaryKeyField];
      await updateSmartObjectRecord(selectedSO.id, pkVal, recordFormData);
    }
    setRecordModalMode(null);
    setEditingRecord(null);
  };

  const handleDeleteRecord = async () => {
    if (!selectedSO || !deleteRecordTarget) return;
    const pkVal = deleteRecordTarget[primaryKeyField];
    await deleteSmartObjectRecord(selectedSO.id, pkVal);
    setDeleteRecordTarget(null);
  };

  return (
    <div className="flex-1 overflow-y-auto p-6 space-y-6 select-none bg-slate-50/50 dark:bg-[#0B1120] min-h-screen">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-extrabold text-[#0F172A] dark:text-white flex items-center gap-2">
            <Database className="w-5 h-5 text-[#2563EB] dark:text-blue-400" />
            SmartObjects Data Models & Schema Builder
          </h2>
          <p className="text-xs text-[#64748B] dark:text-slate-400 font-medium">
            Tamam entity catalog binding process variables to enterprise SQL & REST APIs
          </p>
        </div>

        <div className="flex items-center gap-2">
          {selectedSO && (
            <button
              type="button"
              onClick={async () => {
                await generateFromSmartObject(selectedSO);
                setCurrentTab('smartforms');
              }}
              data-testid="generate-smartform-from-datamodel-btn"
              className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-xs font-bold flex items-center gap-1.5 shadow-md cursor-pointer transition-all active:scale-95"
            >
              <Sparkles className="w-4 h-4" />
              <span>Generate SmartForm</span>
            </button>
          )}

          <button 
            onClick={openCreateEntity}
            data-testid="create-smartobject-btn"
            className="px-4 py-2 bg-[#2563EB] hover:bg-blue-700 text-white rounded-xl text-xs font-bold flex items-center gap-1.5 shadow-md cursor-pointer transition-all active:scale-95"
          >
            <Plus className="w-4 h-4" />
            <span>New SmartObject Entity</span>
          </button>
        </div>
      </div>

      {/* Main Grid Workspace */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left: SmartObjects Directory List */}
        <div className="space-y-3">
          <div className="flex items-center justify-between px-1">
            <span className="text-xs font-bold uppercase tracking-wider text-[#64748B] dark:text-slate-400 block">
              Registered SmartObjects ({smartObjects.length})
            </span>
          </div>

          {smartObjects.map((so) => (
            <div
              key={so.id}
              onClick={() => setSelectedSOId(so.id)}
              data-testid={`smartobject-card-${so.id}`}
              className={`p-4 rounded-2xl border transition-all cursor-pointer relative group ${
                selectedSO?.id === so.id
                  ? 'border-[#2563EB] dark:border-blue-500 bg-blue-50/50 dark:bg-slate-800/80 shadow-sm'
                  : 'border-[#E2E8F0] dark:border-[#334155] bg-white dark:bg-[#1E293B] hover:border-slate-300 dark:hover:border-slate-700'
              }`}
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <div className={`p-2 rounded-xl border ${
                    selectedSO?.id === so.id 
                      ? 'bg-blue-600 text-white border-blue-600' 
                      : 'bg-white dark:bg-slate-900 border-slate-200 dark:border-slate-700 text-[#2563EB] dark:text-blue-400'
                  }`}>
                    <Table className="w-4 h-4" />
                  </div>
                  <div>
                    <h4 className="text-xs font-extrabold text-[#0F172A] dark:text-white">{so.displayName}</h4>
                    <span className="text-[10px] font-mono text-[#64748B] dark:text-slate-400 font-semibold">{so.name}</span>
                  </div>
                </div>

                <span className="text-[10px] font-mono font-bold px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300">
                  {so.category}
                </span>
              </div>

              <p className="text-[11px] text-[#64748B] dark:text-slate-400 mt-2 font-medium leading-relaxed line-clamp-2">
                {so.description || 'No description provided.'}
              </p>

              <div className="flex items-center justify-between mt-3 pt-2 border-t border-slate-100 dark:border-slate-800 text-[10px] text-[#64748B] dark:text-slate-400 font-mono">
                <span>Fields: {so.fields.length}</span>
                <span>Records: {(smartObjectData[so.id]?.length ?? so.recordsCount).toLocaleString()}</span>
              </div>
            </div>
          ))}

          {smartObjects.length === 0 && (
            <div className="p-8 text-center border-2 border-dashed border-slate-200 dark:border-slate-800 rounded-2xl">
              <Database className="w-8 h-8 text-slate-400 mx-auto mb-2 opacity-50" />
              <p className="text-xs font-bold text-slate-600 dark:text-slate-300">No SmartObjects registered</p>
              <button 
                onClick={openCreateEntity}
                className="mt-3 text-xs text-blue-600 dark:text-blue-400 font-bold hover:underline"
              >
                + Create First SmartObject
              </button>
            </div>
          )}
        </div>

        {/* Right: Selected SmartObject Inspector & Tabs */}
        {selectedSO ? (
          <div className="lg:col-span-2 space-y-4">
            <div className="p-6 rounded-3xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs space-y-5 transition-colors">
              {/* Entity Title & Header Actions */}
              <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-4 pb-4 border-b border-[#E2E8F0] dark:border-[#334155]">
                <div>
                  <div className="flex items-center gap-2">
                    <span className="text-[10px] font-mono font-bold text-[#2563EB] uppercase tracking-wider bg-blue-50 dark:bg-blue-950 px-2 py-0.5 rounded border border-blue-200 dark:border-blue-900">
                      {selectedSO.category} SmartObject
                    </span>
                    <span className="text-[10px] font-mono text-slate-400">ID: {selectedSO.id}</span>
                  </div>
                  <h3 className="text-lg font-extrabold text-[#0F172A] dark:text-white mt-1">
                    {selectedSO.displayName} <span className="text-xs font-mono font-normal text-slate-400">({selectedSO.name})</span>
                  </h3>
                  <p className="text-xs text-[#64748B] dark:text-slate-400 font-medium mt-0.5">
                    {selectedSO.description || 'No description provided.'}
                  </p>
                </div>

                <div className="flex items-center gap-2 flex-wrap">
                  <button 
                    onClick={openEditEntity}
                    data-testid="edit-smartobject-btn"
                    className="px-3 py-1.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-xs font-bold flex items-center gap-1.5 shadow-2xs hover:bg-slate-50 dark:hover:bg-slate-700/50 cursor-pointer text-slate-700 dark:text-slate-200"
                    title="Edit Entity Metadata"
                  >
                    <Edit2 className="w-3.5 h-3.5 text-blue-600 dark:text-blue-400" />
                    <span>Edit</span>
                  </button>

                  <button 
                    onClick={() => setIsDeleteEntityConfirmOpen(true)}
                    data-testid="delete-smartobject-btn"
                    className="px-3 py-1.5 rounded-xl border border-red-200 dark:border-red-900/60 bg-red-50/50 dark:bg-red-950/30 text-xs font-bold flex items-center gap-1.5 shadow-2xs hover:bg-red-100 dark:hover:bg-red-900/50 cursor-pointer text-red-600 dark:text-red-400"
                    title="Delete Entity"
                  >
                    <Trash2 className="w-3.5 h-3.5 text-red-600 dark:text-red-400" />
                    <span>Delete</span>
                  </button>

                  <div className="h-4 w-px bg-slate-200 dark:bg-slate-700 mx-1 hidden sm:block" />

                  <button className="px-3 py-1.5 rounded-xl border border-[#E2E8F0] dark:border-slate-700 bg-white dark:bg-slate-800 text-xs font-bold flex items-center gap-1.5 shadow-2xs text-slate-700 dark:text-slate-200">
                    <Server className="w-3.5 h-3.5 text-[#2563EB]" />
                    <span>Connector Config</span>
                  </button>
                </div>
              </div>

              {/* Navigation Tabs: Schema vs Records */}
              <div className="flex items-center gap-2 border-b border-[#E2E8F0] dark:border-slate-800 pb-2">
                <button
                  onClick={() => setActiveTab('schema')}
                  data-testid="tab-schema"
                  className={`px-4 py-2 rounded-xl text-xs font-bold flex items-center gap-2 transition-all cursor-pointer ${
                    activeTab === 'schema'
                      ? 'bg-blue-50 dark:bg-blue-950 text-blue-600 dark:text-blue-400 border border-blue-200 dark:border-blue-900 shadow-2xs'
                      : 'text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800'
                  }`}
                >
                  <Layers className="w-4 h-4" />
                  <span>Schema & Fields</span>
                  <span className="px-1.5 py-0.5 rounded-full text-[10px] bg-slate-200 dark:bg-slate-800 text-slate-700 dark:text-slate-300 font-mono">
                    {selectedSO.fields.length}
                  </span>
                </button>

                <button
                  onClick={() => setActiveTab('records')}
                  data-testid="tab-records"
                  className={`px-4 py-2 rounded-xl text-xs font-bold flex items-center gap-2 transition-all cursor-pointer ${
                    activeTab === 'records'
                      ? 'bg-blue-50 dark:bg-blue-950 text-blue-600 dark:text-blue-400 border border-blue-200 dark:border-blue-900 shadow-2xs'
                      : 'text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800'
                  }`}
                >
                  <Table className="w-4 h-4" />
                  <span>Data Items (Records)</span>
                  <span className="px-1.5 py-0.5 rounded-full text-[10px] bg-slate-200 dark:bg-slate-800 text-slate-700 dark:text-slate-300 font-mono">
                    {currentRecords.length}
                  </span>
                </button>
              </div>

              {/* TAB 1: SCHEMA & FIELDS */}
              {activeTab === 'schema' && (
                <div className="space-y-4">
                  <div className="flex items-center justify-between">
                    <h4 className="text-xs font-extrabold text-[#0F172A] dark:text-white flex items-center gap-1.5">
                      <Layers className="w-4 h-4 text-[#2563EB]" />
                      Field Property Schema Definitions ({selectedSO.fields.length})
                    </h4>

                    <button
                      onClick={() => setIsAddFieldOpen(true)}
                      data-testid="add-field-btn"
                      className="px-3 py-1.5 bg-blue-50 dark:bg-blue-950 border border-blue-200 dark:border-blue-800 text-blue-600 dark:text-blue-400 rounded-xl text-xs font-bold flex items-center gap-1.5 hover:bg-blue-100 cursor-pointer transition-colors"
                    >
                      <Plus className="w-3.5 h-3.5" />
                      <span>Add Field</span>
                    </button>
                  </div>

                  <div className="overflow-x-auto border border-[#E2E8F0] dark:border-slate-800 rounded-2xl">
                    <table className="w-full text-left border-collapse text-xs">
                      <thead>
                        <tr className="bg-[#F8FAFC] dark:bg-slate-900/80 border-b border-[#E2E8F0] dark:border-slate-800 text-[10px] font-bold uppercase tracking-wider text-[#64748B] dark:text-slate-400">
                          <th className="p-3">Field Name</th>
                          <th className="p-3">Data Type</th>
                          <th className="p-3">Primary Key</th>
                          <th className="p-3">Required</th>
                          <th className="p-3 text-right">Actions</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-[#E2E8F0] dark:divide-slate-800">
                        {selectedSO.fields.map((f, i) => (
                          <tr key={i} className="hover:bg-slate-50 dark:hover:bg-slate-800/50 transition-colors">
                            <td className="p-3 font-mono font-extrabold text-[#0F172A] dark:text-slate-100">
                              {f.name}
                            </td>
                            <td className="p-3 font-mono text-[11px] text-[#2563EB] dark:text-blue-400 font-bold">
                              {f.type}
                            </td>
                            <td className="p-3">
                              {f.isPrimaryKey ? (
                                <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-amber-50 dark:bg-amber-950 text-amber-700 dark:text-amber-400 border border-amber-200 dark:border-amber-800">
                                  <Key className="w-3 h-3" /> PK
                                </span>
                              ) : (
                                <span className="text-slate-400 font-mono text-[10px]">-</span>
                              )}
                            </td>
                            <td className="p-3">
                              {f.isRequired ? (
                                <span className="inline-flex items-center gap-1 text-emerald-600 dark:text-emerald-400 font-bold text-[10px]">
                                  <Check className="w-3.5 h-3.5" /> Required
                                </span>
                              ) : (
                                <span className="text-slate-400 text-[10px]">Optional</span>
                              )}
                            </td>
                            <td className="p-3 text-right">
                              <button
                                onClick={() => deleteSmartObjectField(selectedSO.id, f.name)}
                                disabled={f.isPrimaryKey}
                                title={f.isPrimaryKey ? 'Primary key field cannot be deleted' : 'Delete field'}
                                className={`p-1.5 rounded-lg transition-colors cursor-pointer ${
                                  f.isPrimaryKey 
                                    ? 'opacity-30 cursor-not-allowed text-slate-400' 
                                    : 'text-slate-400 hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-950/50'
                                }`}
                              >
                                <Trash2 className="w-3.5 h-3.5" />
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}

              {/* TAB 2: DATA ITEMS (RECORDS) */}
              {activeTab === 'records' && (
                <div className="space-y-4">
                  {/* Top Bar: Search, Refresh, Add Record */}
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                    <div className="flex items-center gap-2 flex-1 max-w-sm">
                      <div className="relative flex-1">
                        <Search className="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
                        <input
                          type="text"
                          value={searchTerm}
                          onChange={(e) => setSearchTerm(e.target.value)}
                          placeholder="Search data items..."
                          data-testid="search-records-input"
                          className="w-full pl-9 pr-3 py-1.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                        />
                      </div>

                      <button
                        onClick={handleRefreshRecords}
                        disabled={isFetchingRecords}
                        data-testid="refresh-records-btn"
                        className="p-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-600 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-700 cursor-pointer transition-all"
                        title="Fetch / Refresh Records from Repository"
                      >
                        <RefreshCw className={`w-3.5 h-3.5 ${isFetchingRecords ? 'animate-spin text-blue-600' : ''}`} />
                      </button>
                    </div>

                    <div className="flex items-center gap-2">
                      <span className="text-[11px] font-mono text-slate-500">
                        Showing {filteredRecords.length} of {currentRecords.length} records
                      </span>

                      <button
                        onClick={openAddRecordModal}
                        data-testid="add-record-btn"
                        className="px-3 py-1.5 bg-[#2563EB] hover:bg-blue-700 text-white rounded-xl text-xs font-bold flex items-center gap-1.5 shadow-xs cursor-pointer transition-all active:scale-95"
                      >
                        <Plus className="w-3.5 h-3.5" />
                        <span>Add Record</span>
                      </button>
                    </div>
                  </div>

                  {/* Records Table */}
                  <div className="overflow-x-auto border border-[#E2E8F0] dark:border-slate-800 rounded-2xl max-h-[480px]">
                    <table className="w-full text-left border-collapse text-xs" data-testid="smartobject-records-table">
                      <thead className="sticky top-0 bg-[#F8FAFC] dark:bg-slate-900 z-10">
                        <tr className="border-b border-[#E2E8F0] dark:border-slate-800 text-[10px] font-bold uppercase tracking-wider text-[#64748B] dark:text-slate-400">
                          {selectedSO.fields.map(f => (
                            <th key={f.name} className="p-3">
                              <span className="flex items-center gap-1">
                                {f.name}
                                {f.isPrimaryKey && <Key className="w-3 h-3 text-amber-500" />}
                              </span>
                            </th>
                          ))}
                          <th className="p-3 text-right">Actions</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-[#E2E8F0] dark:divide-slate-800">
                        {filteredRecords.map((rec, rIdx) => {
                          const pkVal = rec[primaryKeyField];
                          return (
                            <tr 
                              key={pkVal ?? rIdx} 
                              data-testid={`record-row-${pkVal ?? rIdx}`}
                              className="hover:bg-slate-50 dark:hover:bg-slate-800/50 transition-colors"
                            >
                              {selectedSO.fields.map(f => {
                                const val = rec[f.name];
                                return (
                                  <td key={f.name} className="p-3 font-mono text-xs text-[#0F172A] dark:text-slate-200">
                                    {f.isPrimaryKey ? (
                                      <span className="font-extrabold text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-950/60 px-1.5 py-0.5 rounded border border-blue-200 dark:border-blue-900">
                                        {String(val ?? '')}
                                      </span>
                                    ) : f.type === 'Boolean' ? (
                                      <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                                        val 
                                          ? 'bg-emerald-50 dark:bg-emerald-950 text-emerald-600 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-900' 
                                          : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400'
                                      }`}>
                                        {val ? 'TRUE' : 'FALSE'}
                                      </span>
                                    ) : f.type === 'Number' ? (
                                      typeof val === 'number' ? val.toLocaleString() : String(val ?? '')
                                    ) : (
                                      String(val ?? '')
                                    )}
                                  </td>
                                );
                              })}
                              <td className="p-3 text-right">
                                <div className="flex items-center justify-end gap-1">
                                  <button
                                    onClick={() => openEditRecordModal(rec)}
                                    data-testid={`edit-record-${pkVal}`}
                                    className="p-1.5 text-slate-400 hover:text-blue-600 hover:bg-blue-50 dark:hover:bg-slate-800 rounded-lg transition-colors cursor-pointer"
                                    title="Edit Record"
                                  >
                                    <Edit2 className="w-3.5 h-3.5" />
                                  </button>
                                  <button
                                    onClick={() => setDeleteRecordTarget(rec)}
                                    data-testid={`delete-record-${pkVal}`}
                                    className="p-1.5 text-slate-400 hover:text-red-600 hover:bg-red-50 dark:hover:bg-slate-800 rounded-lg transition-colors cursor-pointer"
                                    title="Delete Record"
                                  >
                                    <Trash2 className="w-3.5 h-3.5" />
                                  </button>
                                </div>
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>

                    {filteredRecords.length === 0 && (
                      <div className="p-8 text-center">
                        <FileText className="w-8 h-8 text-slate-400 mx-auto mb-2 opacity-50" />
                        <p className="text-xs font-bold text-slate-600 dark:text-slate-300">No data items found</p>
                        <p className="text-[11px] text-slate-400 mt-1">
                          {searchTerm ? 'No records match your search filter.' : 'Click "+ Add Record" to add the first data item.'}
                        </p>
                      </div>
                    )}
                  </div>
                </div>
              )}
            </div>
          </div>
        ) : (
          <div className="lg:col-span-2 p-12 text-center border-2 border-dashed border-slate-200 dark:border-slate-800 rounded-3xl">
            <Database className="w-10 h-10 text-slate-400 mx-auto mb-3 opacity-50" />
            <h3 className="text-sm font-bold text-slate-700 dark:text-slate-300">Select or create a SmartObject</h3>
            <p className="text-xs text-slate-500 mt-1">Choose an entity on the left to inspect its schema and data items.</p>
          </div>
        )}
      </div>

      {/* MODAL 1: CREATE SMARTOBJECT ENTITY */}
      {isCreateEntityOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-4">
          <div className="bg-white dark:bg-[#1E293B] border border-slate-200 dark:border-slate-700 rounded-3xl max-w-lg w-full p-6 shadow-2xl space-y-5 animate-in fade-in zoom-in-95 duration-150">
            <div className="flex items-center justify-between pb-3 border-b border-slate-200 dark:border-slate-700">
              <div className="flex items-center gap-2">
                <Database className="w-5 h-5 text-blue-600" />
                <h3 className="text-sm font-extrabold text-slate-900 dark:text-white">Create SmartObject Entity</h3>
              </div>
              <button 
                onClick={() => setIsCreateEntityOpen(false)}
                className="p-1 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 rounded-lg cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <form onSubmit={handleCreateEntitySubmit} className="space-y-4">
              <div>
                <label className="block text-[11px] font-bold text-slate-700 dark:text-slate-300 mb-1">
                  Entity Technical Name (Alphanumeric identifier) *
                </label>
                <input
                  type="text"
                  required
                  value={newEntityName}
                  onChange={(e) => {
                    const val = e.target.value;
                    setNewEntityName(val);
                    if (!newEntityDisplayName) {
                      setNewEntityDisplayName(val);
                    }
                  }}
                  placeholder="e.g. CustomerAccount, VendorInvoice"
                  data-testid="new-entity-name-input"
                  className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs font-mono text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                />
              </div>

              <div>
                <label className="block text-[11px] font-bold text-slate-700 dark:text-slate-300 mb-1">
                  Display Name
                </label>
                <input
                  type="text"
                  value={newEntityDisplayName}
                  onChange={(e) => setNewEntityDisplayName(e.target.value)}
                  placeholder="e.g. Customer Account SmartObject"
                  data-testid="new-entity-displayname-input"
                  className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-[11px] font-bold text-slate-700 dark:text-slate-300 mb-1">
                    Category
                  </label>
                  <select
                    value={newEntityCategory}
                    onChange={(e) => setNewEntityCategory(e.target.value as SmartObjectCategory)}
                    data-testid="new-entity-category-select"
                    className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                  >
                    <option value="HR">HR</option>
                    <option value="Finance">Finance</option>
                    <option value="IT">IT</option>
                    <option value="Core">Core</option>
                    <option value="Custom">Custom</option>
                  </select>
                </div>

                <div>
                  <label className="block text-[11px] font-bold text-slate-700 dark:text-slate-300 mb-1">
                    Primary Key Type
                  </label>
                  <select
                    value={newEntityPkType}
                    onChange={(e) => setNewEntityPkType(e.target.value as any)}
                    className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                  >
                    <option value="GUID">GUID (UUID)</option>
                    <option value="Number">Number (Auto ID)</option>
                    <option value="String">String (Code)</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="block text-[11px] font-bold text-slate-700 dark:text-slate-300 mb-1">
                  Primary Key Field Name
                </label>
                <input
                  type="text"
                  value={newEntityPkName}
                  onChange={(e) => setNewEntityPkName(e.target.value)}
                  placeholder="e.g. Id, CustomerID"
                  className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs font-mono text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                />
              </div>

              <div>
                <label className="block text-[11px] font-bold text-slate-700 dark:text-slate-300 mb-1">
                  Description
                </label>
                <textarea
                  rows={2}
                  value={newEntityDescription}
                  onChange={(e) => setNewEntityDescription(e.target.value)}
                  placeholder="Entity description and business purpose..."
                  className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                />
              </div>

              <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-200 dark:border-slate-700">
                <button
                  type="button"
                  onClick={() => setIsCreateEntityOpen(false)}
                  className="px-4 py-2 rounded-xl border border-slate-200 dark:border-slate-700 text-xs font-bold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  data-testid="submit-create-entity-btn"
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-xl text-xs font-bold shadow-md cursor-pointer transition-all"
                >
                  Create Entity
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 2: EDIT SMARTOBJECT ENTITY */}
      {isEditEntityOpen && selectedSO && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-4">
          <div className="bg-white dark:bg-[#1E293B] border border-slate-200 dark:border-slate-700 rounded-3xl max-w-lg w-full p-6 shadow-2xl space-y-5 animate-in fade-in zoom-in-95 duration-150">
            <div className="flex items-center justify-between pb-3 border-b border-slate-200 dark:border-slate-700">
              <div className="flex items-center gap-2">
                <Edit2 className="w-5 h-5 text-blue-600" />
                <h3 className="text-sm font-extrabold text-slate-900 dark:text-white">Edit SmartObject: {selectedSO.name}</h3>
              </div>
              <button 
                onClick={() => setIsEditEntityOpen(false)}
                className="p-1 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 rounded-lg cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <form onSubmit={handleEditEntitySubmit} className="space-y-4">
              <div>
                <label className="block text-[11px] font-bold text-slate-700 dark:text-slate-300 mb-1">
                  Display Name
                </label>
                <input
                  type="text"
                  required
                  value={editDisplayName}
                  onChange={(e) => setEditDisplayName(e.target.value)}
                  data-testid="edit-entity-displayname-input"
                  className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                />
              </div>

              <div>
                <label className="block text-[11px] font-bold text-slate-700 dark:text-slate-300 mb-1">
                  Category
                </label>
                <select
                  value={editCategory}
                  onChange={(e) => setEditCategory(e.target.value as SmartObjectCategory)}
                  data-testid="edit-entity-category-select"
                  className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                >
                  <option value="HR">HR</option>
                  <option value="Finance">Finance</option>
                  <option value="IT">IT</option>
                  <option value="Core">Core</option>
                  <option value="Custom">Custom</option>
                </select>
              </div>

              <div>
                <label className="block text-[11px] font-bold text-slate-700 dark:text-slate-300 mb-1">
                  Description
                </label>
                <textarea
                  rows={3}
                  value={editDescription}
                  onChange={(e) => setEditDescription(e.target.value)}
                  data-testid="edit-entity-description-input"
                  className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                />
              </div>

              <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-200 dark:border-slate-700">
                <button
                  type="button"
                  onClick={() => setIsEditEntityOpen(false)}
                  className="px-4 py-2 rounded-xl border border-slate-200 dark:border-slate-700 text-xs font-bold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  data-testid="submit-edit-entity-btn"
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-xl text-xs font-bold shadow-md cursor-pointer transition-all"
                >
                  Save Changes
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 3: DELETE ENTITY CONFIRM */}
      {isDeleteEntityConfirmOpen && selectedSO && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-4">
          <div className="bg-white dark:bg-[#1E293B] border border-slate-200 dark:border-slate-700 rounded-3xl max-w-md w-full p-6 shadow-2xl space-y-4 animate-in fade-in zoom-in-95 duration-150">
            <div className="flex items-center gap-3 text-red-600 dark:text-red-400">
              <div className="p-2.5 rounded-2xl bg-red-100 dark:bg-red-950/60">
                <AlertCircle className="w-6 h-6" />
              </div>
              <div>
                <h3 className="text-sm font-extrabold text-slate-900 dark:text-white">Delete SmartObject Entity?</h3>
                <p className="text-xs text-slate-500">This action cannot be undone.</p>
              </div>
            </div>

            <p className="text-xs text-slate-600 dark:text-slate-300 leading-relaxed">
              Are you sure you want to delete <span className="font-bold text-slate-900 dark:text-white">"{selectedSO.displayName}"</span> ({selectedSO.name}) and all its associated schema definitions and data records?
            </p>

            <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-200 dark:border-slate-700">
              <button
                type="button"
                onClick={() => setIsDeleteEntityConfirmOpen(false)}
                className="px-4 py-2 rounded-xl border border-slate-200 dark:border-slate-700 text-xs font-bold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleDeleteEntitySubmit}
                data-testid="confirm-delete-entity-btn"
                className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-xl text-xs font-bold shadow-md cursor-pointer transition-all"
              >
                Confirm Delete
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 4: ADD FIELD MODAL */}
      {isAddFieldOpen && selectedSO && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-4">
          <div className="bg-white dark:bg-[#1E293B] border border-slate-200 dark:border-slate-700 rounded-3xl max-w-md w-full p-6 shadow-2xl space-y-4 animate-in fade-in zoom-in-95 duration-150">
            <div className="flex items-center justify-between pb-3 border-b border-slate-200 dark:border-slate-700">
              <div className="flex items-center gap-2">
                <Layers className="w-5 h-5 text-blue-600" />
                <h3 className="text-sm font-extrabold text-slate-900 dark:text-white">Add Field to {selectedSO.displayName}</h3>
              </div>
              <button 
                onClick={() => setIsAddFieldOpen(false)}
                className="p-1 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 rounded-lg cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <form onSubmit={handleAddFieldSubmit} className="space-y-4">
              <div>
                <label className="block text-[11px] font-bold text-slate-700 dark:text-slate-300 mb-1">
                  Field Property Name *
                </label>
                <input
                  type="text"
                  required
                  value={newFieldName}
                  onChange={(e) => setNewFieldName(e.target.value)}
                  placeholder="e.g. EmailAddress, TotalCost"
                  data-testid="new-field-name-input"
                  className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs font-mono text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                />
              </div>

              <div>
                <label className="block text-[11px] font-bold text-slate-700 dark:text-slate-300 mb-1">
                  Data Type
                </label>
                <select
                  value={newFieldType}
                  onChange={(e) => setNewFieldType(e.target.value as any)}
                  data-testid="new-field-type-select"
                  className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                >
                  <option value="String">String (Text)</option>
                  <option value="Number">Number (Integer / Decimal)</option>
                  <option value="Boolean">Boolean (True / False)</option>
                  <option value="DateTime">DateTime (Date & Time)</option>
                  <option value="GUID">GUID (UUID)</option>
                </select>
              </div>

              <div className="flex items-center gap-6 pt-1">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={newFieldIsPk}
                    onChange={(e) => setNewFieldIsPk(e.target.checked)}
                    className="w-4 h-4 rounded border-slate-300 text-blue-600 focus:ring-blue-500"
                  />
                  <span className="text-xs font-bold text-slate-700 dark:text-slate-300">Primary Key</span>
                </label>

                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={newFieldIsRequired}
                    onChange={(e) => setNewFieldIsRequired(e.target.checked)}
                    className="w-4 h-4 rounded border-slate-300 text-blue-600 focus:ring-blue-500"
                  />
                  <span className="text-xs font-bold text-slate-700 dark:text-slate-300">Required</span>
                </label>
              </div>

              <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-200 dark:border-slate-700">
                <button
                  type="button"
                  onClick={() => setIsAddFieldOpen(false)}
                  className="px-4 py-2 rounded-xl border border-slate-200 dark:border-slate-700 text-xs font-bold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  data-testid="submit-add-field-btn"
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-xl text-xs font-bold shadow-md cursor-pointer transition-all"
                >
                  Add Field
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 5: ADD / EDIT DATA ITEM (RECORD) */}
      {recordModalMode && selectedSO && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-4">
          <div className="bg-white dark:bg-[#1E293B] border border-slate-200 dark:border-slate-700 rounded-3xl max-w-lg w-full p-6 shadow-2xl space-y-5 animate-in fade-in zoom-in-95 duration-150 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between pb-3 border-b border-slate-200 dark:border-slate-700">
              <div className="flex items-center gap-2">
                <Table className="w-5 h-5 text-blue-600" />
                <h3 className="text-sm font-extrabold text-slate-900 dark:text-white">
                  {recordModalMode === 'add' ? 'Add New Data Item' : 'Edit Data Item'}
                </h3>
              </div>
              <button 
                onClick={() => setRecordModalMode(null)}
                className="p-1 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 rounded-lg cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <form onSubmit={handleRecordFormSubmit} className="space-y-4">
              {selectedSO.fields.map(f => {
                const isPkAndEdit = recordModalMode === 'edit' && f.isPrimaryKey;

                return (
                  <div key={f.name}>
                    <div className="flex items-center justify-between mb-1">
                      <label className="text-[11px] font-bold text-slate-700 dark:text-slate-300 flex items-center gap-1.5">
                        <span>{f.name}</span>
                        {f.isPrimaryKey && (
                          <span className="px-1.5 py-0.2 rounded text-[9px] font-mono font-bold bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-400 border border-amber-200 dark:border-amber-800">
                            PK
                          </span>
                        )}
                        {f.isRequired && <span className="text-red-500">*</span>}
                      </label>
                      <span className="text-[10px] font-mono text-slate-400">{f.type}</span>
                    </div>

                    {f.type === 'Boolean' ? (
                      <select
                        value={recordFormData[f.name] ? 'true' : 'false'}
                        onChange={(e) => setRecordFormData({ ...recordFormData, [f.name]: e.target.value === 'true' })}
                        data-testid={`record-field-${f.name}`}
                        className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                      >
                        <option value="true">True</option>
                        <option value="false">False</option>
                      </select>
                    ) : f.type === 'Number' ? (
                      <input
                        type="number"
                        required={f.isRequired}
                        value={recordFormData[f.name] ?? ''}
                        onChange={(e) => setRecordFormData({ ...recordFormData, [f.name]: e.target.value === '' ? '' : Number(e.target.value) })}
                        data-testid={`record-field-${f.name}`}
                        className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs font-mono text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                      />
                    ) : f.type === 'DateTime' ? (
                      <input
                        type="date"
                        required={f.isRequired}
                        value={recordFormData[f.name] ?? ''}
                        onChange={(e) => setRecordFormData({ ...recordFormData, [f.name]: e.target.value })}
                        data-testid={`record-field-${f.name}`}
                        className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs font-mono text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                      />
                    ) : f.type === 'GUID' ? (
                      <div className="flex gap-2">
                        <input
                          type="text"
                          required={f.isRequired}
                          disabled={isPkAndEdit}
                          value={recordFormData[f.name] ?? ''}
                          onChange={(e) => setRecordFormData({ ...recordFormData, [f.name]: e.target.value })}
                          data-testid={`record-field-${f.name}`}
                          className={`w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs font-mono text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500 ${
                            isPkAndEdit ? 'opacity-60 bg-slate-100 dark:bg-slate-800 cursor-not-allowed' : ''
                          }`}
                        />
                        {!isPkAndEdit && (
                          <button
                            type="button"
                            onClick={() => setRecordFormData({ ...recordFormData, [f.name]: `id-${Math.random().toString(36).substring(2, 9)}` })}
                            className="px-2.5 py-1.5 rounded-xl border border-slate-200 dark:border-slate-700 text-[10px] font-bold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 whitespace-nowrap"
                            title="Generate UUID"
                          >
                            <Sparkles className="w-3.5 h-3.5" />
                          </button>
                        )}
                      </div>
                    ) : (
                      <input
                        type="text"
                        required={f.isRequired}
                        disabled={isPkAndEdit}
                        value={recordFormData[f.name] ?? ''}
                        onChange={(e) => setRecordFormData({ ...recordFormData, [f.name]: e.target.value })}
                        data-testid={`record-field-${f.name}`}
                        className={`w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs font-mono text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500 ${
                          isPkAndEdit ? 'opacity-60 bg-slate-100 dark:bg-slate-800 cursor-not-allowed' : ''
                        }`}
                      />
                    )}
                  </div>
                );
              })}

              <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-200 dark:border-slate-700">
                <button
                  type="button"
                  onClick={() => setRecordModalMode(null)}
                  className="px-4 py-2 rounded-xl border border-slate-200 dark:border-slate-700 text-xs font-bold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  data-testid="submit-record-form-btn"
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-xl text-xs font-bold shadow-md cursor-pointer transition-all"
                >
                  {recordModalMode === 'add' ? 'Add Record' : 'Save Changes'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 6: DELETE RECORD CONFIRM */}
      {deleteRecordTarget && selectedSO && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-4">
          <div className="bg-white dark:bg-[#1E293B] border border-slate-200 dark:border-slate-700 rounded-3xl max-w-md w-full p-6 shadow-2xl space-y-4 animate-in fade-in zoom-in-95 duration-150">
            <div className="flex items-center gap-3 text-red-600 dark:text-red-400">
              <div className="p-2.5 rounded-2xl bg-red-100 dark:bg-red-950/60">
                <AlertCircle className="w-6 h-6" />
              </div>
              <div>
                <h3 className="text-sm font-extrabold text-slate-900 dark:text-white">Delete Data Item?</h3>
                <p className="text-xs text-slate-500">Record ID: {String(deleteRecordTarget[primaryKeyField])}</p>
              </div>
            </div>

            <p className="text-xs text-slate-600 dark:text-slate-300 leading-relaxed">
              Are you sure you want to permanently remove this record from <span className="font-bold text-slate-900 dark:text-white">{selectedSO.displayName}</span>?
            </p>

            <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-200 dark:border-slate-700">
              <button
                type="button"
                onClick={() => setDeleteRecordTarget(null)}
                className="px-4 py-2 rounded-xl border border-slate-200 dark:border-slate-700 text-xs font-bold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleDeleteRecord}
                data-testid="confirm-delete-record-btn"
                className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-xl text-xs font-bold shadow-md cursor-pointer transition-all"
              >
                Delete Record
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
