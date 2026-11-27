import React, { useEffect, useState, useMemo } from 'react';
import {
  Database,
  X,
  RefreshCw,
  AlertTriangle,
  Key,
  EyeOff,
  ChevronRight,
  ChevronLeft,
  CheckCircle2,
  ShieldAlert
} from 'lucide-react';
import { getWorkflowRepository } from '../../services/repositoryManager';
import type {
  ConnectionRecord,
  DiscoveredEntity,
  SmartObjectEntity,
  SelectedSmartObjectFieldPayload
} from '../../types/workflow';

interface ColumnDraft {
  name: string;
  displayName: string;
  sourceType: string;
  type: string;
  isNullable: boolean;
  isPrimaryKey: boolean;
  suggestedMasked: boolean;
  included: boolean;
  masked: boolean;
}

export interface ImportSmartObjectWizardModalProps {
  isOpen: boolean;
  onClose: () => void;
  onGenerated: (smartObject: SmartObjectEntity) => void;
  /** Optional pre-loaded connections (useful for tests or parent caching). */
  initialConnections?: ConnectionRecord[];
}

export const ImportSmartObjectWizardModal: React.FC<ImportSmartObjectWizardModalProps> = ({
  isOpen,
  onClose,
  onGenerated,
  initialConnections
}) => {
  const [step, setStep] = useState<1 | 2>(1);
  const [connections, setConnections] = useState<ConnectionRecord[]>(initialConnections ?? []);
  const [selectedConnectionId, setSelectedConnectionId] = useState<string>('');
  const [isLoadingConnections, setIsLoadingConnections] = useState(false);

  const [entities, setEntities] = useState<DiscoveredEntity[]>([]);
  const [isDiscovering, setIsDiscovering] = useState(false);
  const [selectedEntityName, setSelectedEntityName] = useState<string>('');

  // Step 2 state
  const [smartObjectKey, setSmartObjectKey] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [primaryKeyField, setPrimaryKeyField] = useState<string>('');
  const [columns, setColumns] = useState<ColumnDraft[]>([]);

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!isOpen) return;
    setStep(1);
    setErrorMessage(null);
    setEntities([]);
    setSelectedEntityName('');
    setColumns([]);

    if (initialConnections && initialConnections.length > 0) {
      setConnections(initialConnections);
      setSelectedConnectionId(initialConnections[0].id);
      return;
    }

    let cancelled = false;
    setIsLoadingConnections(true);
    getWorkflowRepository()
      .getConnections()
      .then((list) => {
        if (cancelled) return;
        setConnections(list);
        if (list.length > 0) {
          setSelectedConnectionId(list[0].id);
        }
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setErrorMessage(err instanceof Error ? err.message : 'Impossible de charger les connexions.');
      })
      .finally(() => {
        if (!cancelled) setIsLoadingConnections(false);
      });

    return () => {
      cancelled = true;
    };
  }, [isOpen, initialConnections]);

  const selectedConnection = useMemo(
    () => connections.find((c) => c.id === selectedConnectionId) || null,
    [connections, selectedConnectionId]
  );

  const selectedEntity = useMemo(
    () => entities.find((e) => e.entityName === selectedEntityName) || null,
    [entities, selectedEntityName]
  );

  const includedColumns = useMemo(
    () => columns.filter((c) => c.included),
    [columns]
  );

  const handleDiscover = async (connId?: string) => {
    const targetId = connId || selectedConnectionId;
    if (!targetId) return;
    setErrorMessage(null);
    setIsDiscovering(true);
    setEntities([]);
    setSelectedEntityName('');
    try {
      const discovered = await getWorkflowRepository().discoverConnectionEntities(targetId);
      setEntities(discovered);
      if (discovered.length > 0) {
        setSelectedEntityName(discovered[0].entityName);
      }
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : 'Échec de la découverte du schéma.');
    } finally {
      setIsDiscovering(false);
    }
  };

  const handleProceedToStep2 = () => {
    if (!selectedEntity) return;
    setErrorMessage(null);
    const sanitizedKey = selectedEntity.entityName
      .replace(/[^a-zA-Z0-9_]/g, '_')
      .replace(/^_+/, '');
    setSmartObjectKey(sanitizedKey || selectedEntity.entityName);
    setDisplayName(selectedEntity.displayName || selectedEntity.entityName);
    setPrimaryKeyField(selectedEntity.detectedPrimaryKey || '');
    setColumns(
      selectedEntity.fields.map((f) => ({
        name: f.name,
        displayName: f.name,
        sourceType: f.sourceType,
        type: f.mappedSmartObjectType || 'string',
        isNullable: f.isNullable,
        isPrimaryKey: f.isPrimaryKey,
        suggestedMasked: f.suggestedMasked,
        included: true,
        masked: f.suggestedMasked
      }))
    );
    setStep(2);
  };

  const toggleColumnIncluded = (name: string) => {
    setColumns((prev) =>
      prev.map((col) => {
        if (col.name !== name) return col;
        const nextIncluded = !col.included;
        if (!nextIncluded && primaryKeyField === name) {
          setPrimaryKeyField('');
        }
        return { ...col, included: nextIncluded };
      })
    );
  };

  const toggleColumnMasked = (name: string) => {
    setColumns((prev) =>
      prev.map((col) => (col.name === name ? { ...col, masked: !col.masked } : col))
    );
  };

  const updateColumnType = (name: string, nextType: string) => {
    setColumns((prev) =>
      prev.map((col) => (col.name === name ? { ...col, type: nextType } : col))
    );
  };

  const handleGenerate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedConnectionId || !selectedEntity) return;

    const selectedFields: SelectedSmartObjectFieldPayload[] = includedColumns.map((col) => ({
      name: col.name,
      displayName: col.displayName || col.name,
      type: col.type,
      masked: col.masked,
      isRequired: !col.isNullable || col.name === primaryKeyField
    }));

    if (selectedFields.length === 0) {
      setErrorMessage('Sélectionnez au moins une colonne à exposer dans le SmartObject.');
      return;
    }

    setErrorMessage(null);
    setIsSubmitting(true);
    try {
      const created = await getWorkflowRepository().generateSmartObjectFromConnection(
        selectedConnectionId,
        {
          entityName: selectedEntity.entityName,
          smartObjectKey: smartObjectKey.trim() || selectedEntity.entityName,
          displayName: displayName.trim() || selectedEntity.displayName,
          primaryKeyField: primaryKeyField || null,
          selectedFields
        }
      );
      onGenerated(created);
      onClose();
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : 'Échec de la génération du SmartObject.');
    } finally {
      setIsSubmitting(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="import-so-wizard-title"
      data-testid="import-smartobject-wizard-modal"
    >
      <div className="bg-white dark:bg-[#1E293B] border border-slate-200 dark:border-slate-700 rounded-3xl max-w-4xl w-full max-h-[90vh] flex flex-col shadow-2xl overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-200 dark:border-slate-700">
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-xl bg-blue-50 dark:bg-blue-950 text-blue-600 dark:text-blue-400">
              <Database className="w-5 h-5" />
            </div>
            <div>
              <h3 id="import-so-wizard-title" className="text-sm font-extrabold text-slate-900 dark:text-white">
                Importer un SmartObject depuis une Connexion
              </h3>
              <p className="text-[11px] text-slate-500 dark:text-slate-400">
                Étape {step} sur 2 — {step === 1 ? 'Connexion & entité source' : 'Prévisualisation, clé primaire & masquage'}
              </p>
            </div>
          </div>

          <button
            type="button"
            onClick={onClose}
            aria-label="Fermer l'assistant"
            className="p-1.5 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 rounded-lg cursor-pointer"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Body */}
        <div className="p-6 overflow-y-auto flex-1 space-y-5">
          {errorMessage && (
            <div
              role="alert"
              className="p-3 rounded-xl bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900 text-xs text-red-700 dark:text-red-300 flex items-center gap-2"
            >
              <AlertTriangle className="w-4 h-4 shrink-0" />
              <span>{errorMessage}</span>
            </div>
          )}

          {step === 1 && (
            <div className="space-y-4">
              <div className="flex flex-col sm:flex-row sm:items-end gap-3">
                <div className="flex-1">
                  <label
                    htmlFor="import-so-connection-select"
                    className="block text-[11px] font-bold text-slate-700 dark:text-slate-300 mb-1"
                  >
                    Connexion externe
                  </label>
                  <select
                    id="import-so-connection-select"
                    data-testid="import-connection-select"
                    value={selectedConnectionId}
                    onChange={(e) => {
                      setSelectedConnectionId(e.target.value);
                      setEntities([]);
                      setSelectedEntityName('');
                    }}
                    disabled={isLoadingConnections}
                    className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                  >
                    {connections.length === 0 && (
                      <option value="">Aucune connexion disponible</option>
                    )}
                    {connections.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name} ({(c.serviceType || 'rest').toUpperCase()})
                      </option>
                    ))}
                  </select>
                </div>

                <button
                  type="button"
                  onClick={() => void handleDiscover()}
                  disabled={!selectedConnectionId || isDiscovering}
                  data-testid="discover-entities-btn"
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white rounded-xl text-xs font-bold flex items-center gap-1.5 cursor-pointer transition-colors"
                >
                  <RefreshCw className={`w-3.5 h-3.5 ${isDiscovering ? 'animate-spin' : ''}`} />
                  <span>{isDiscovering ? 'Découverte...' : 'Découvrir les entités'}</span>
                </button>
              </div>

              {selectedConnection && (
                <div className="text-[11px] text-slate-500 dark:text-slate-400 flex items-center gap-3">
                  <span>
                    Type : <strong className="font-mono uppercase">{selectedConnection.serviceType || 'rest'}</strong>
                  </span>
                  <span>
                    Écriture :{' '}
                    <strong>{selectedConnection.allowWrite ? 'Autorisée (CRUD)' : 'Lecture seule (Get/List)'}</strong>
                  </span>
                </div>
              )}

              {entities.length > 0 && (
                <div className="space-y-2">
                  <span className="block text-[11px] font-bold text-slate-700 dark:text-slate-300">
                    Entités / Tables / Listes découvertes ({entities.length})
                  </span>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5 max-h-64 overflow-y-auto pe-1">
                    {entities.map((ent) => {
                      const active = ent.entityName === selectedEntityName;
                      return (
                        <button
                          type="button"
                          key={ent.entityName}
                          onClick={() => setSelectedEntityName(ent.entityName)}
                          data-testid={`discovered-entity-${ent.entityName}`}
                          className={`p-3 rounded-2xl border text-start transition-all cursor-pointer ${
                            active
                              ? 'border-blue-600 bg-blue-50/70 dark:bg-blue-950/40 shadow-2xs'
                              : 'border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 hover:border-slate-300'
                          }`}
                        >
                          <div className="flex items-center justify-between gap-2">
                            <span className="text-xs font-extrabold text-slate-900 dark:text-white truncate">
                              {ent.displayName}
                            </span>
                            <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
                              {ent.fields.length} col.
                            </span>
                          </div>
                          <div className="text-[10px] font-mono text-slate-500 dark:text-slate-400 mt-1">
                            {ent.entityName}
                          </div>
                          <div className="mt-2 flex items-center gap-2 text-[10px]">
                            {ent.detectedPrimaryKey ? (
                              <span className="inline-flex items-center gap-1 text-amber-700 dark:text-amber-400 font-bold">
                                <Key className="w-3 h-3" /> PK : {ent.detectedPrimaryKey}
                              </span>
                            ) : (
                              <span className="inline-flex items-center gap-1 text-orange-600 dark:text-orange-400 font-bold">
                                <AlertTriangle className="w-3 h-3" /> Aucune PK détectée
                              </span>
                            )}
                          </div>
                        </button>
                      );
                    })}
                  </div>
                </div>
              )}
            </div>
          )}

          {step === 2 && selectedEntity && (
            <form id="import-smartobject-form" onSubmit={handleGenerate} className="space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                <div>
                  <label
                    htmlFor="import-so-key-input"
                    className="block text-[11px] font-bold text-slate-700 dark:text-slate-300 mb-1"
                  >
                    Clé du SmartObject *
                  </label>
                  <input
                    id="import-so-key-input"
                    data-testid="import-so-key-input"
                    type="text"
                    required
                    value={smartObjectKey}
                    onChange={(e) => setSmartObjectKey(e.target.value)}
                    className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs font-mono text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                  />
                </div>

                <div>
                  <label
                    htmlFor="import-so-displayname-input"
                    className="block text-[11px] font-bold text-slate-700 dark:text-slate-300 mb-1"
                  >
                    Nom d&apos;affichage
                  </label>
                  <input
                    id="import-so-displayname-input"
                    data-testid="import-so-displayname-input"
                    type="text"
                    value={displayName}
                    onChange={(e) => setDisplayName(e.target.value)}
                    className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                  />
                </div>

                <div>
                  <label
                    htmlFor="import-so-pk-select"
                    className="block text-[11px] font-bold text-slate-700 dark:text-slate-300 mb-1"
                  >
                    Clé primaire
                  </label>
                  <select
                    id="import-so-pk-select"
                    data-testid="import-so-pk-select"
                    value={primaryKeyField}
                    onChange={(e) => setPrimaryKeyField(e.target.value)}
                    className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs font-mono text-slate-800 dark:text-slate-200 focus:outline-none focus:border-blue-500"
                  >
                    <option value="">Aucune clé primaire</option>
                    {includedColumns.map((col) => (
                      <option key={col.name} value={col.name}>
                        {col.name}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              {/* Warning when no primary key is selected */}
              {!primaryKeyField && (
                <div
                  role="alert"
                  data-testid="no-pk-warning"
                  className="p-3 rounded-xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800 text-xs text-amber-800 dark:text-amber-300 flex items-center gap-2 font-medium"
                >
                  <AlertTriangle className="w-4 h-4 shrink-0 text-amber-600 dark:text-amber-400" />
                  <span>
                    Aucune clé primaire sélectionnée : <strong>Update/Delete désactivés</strong> automatiquement.
                  </span>
                </div>
              )}

              {/* Column preview table */}
              <div className="overflow-x-auto border border-slate-200 dark:border-slate-800 rounded-2xl">
                <table className="w-full text-start border-collapse text-xs" data-testid="import-columns-preview-table">
                  <thead>
                    <tr className="bg-slate-50 dark:bg-slate-900/80 border-b border-slate-200 dark:border-slate-800 text-[10px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                      <th className="p-3">Inclure</th>
                      <th className="p-3">Colonne</th>
                      <th className="p-3">Type source</th>
                      <th className="p-3">Type SmartObject</th>
                      <th className="p-3">Masquer (masked)</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
                    {columns.map((col) => (
                      <tr
                        key={col.name}
                        className={`transition-colors ${
                          col.included
                            ? 'hover:bg-slate-50 dark:hover:bg-slate-800/40'
                            : 'opacity-50 bg-slate-50/50 dark:bg-slate-900/30'
                        }`}
                      >
                        <td className="p-3">
                          <input
                            id={`col-include-${col.name}`}
                            type="checkbox"
                            checked={col.included}
                            onChange={() => toggleColumnIncluded(col.name)}
                            aria-label={`Inclure la colonne ${col.name}`}
                            data-testid={`col-include-${col.name}`}
                            className="rounded border-slate-300 text-blue-600 focus:ring-blue-500 cursor-pointer"
                          />
                        </td>
                        <td className="p-3 font-mono font-bold text-slate-900 dark:text-slate-100">
                          <div className="flex items-center gap-1.5">
                            <span>{col.name}</span>
                            {primaryKeyField === col.name && (
                              <span className="inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded text-[9px] bg-amber-100 dark:bg-amber-950 text-amber-700 dark:text-amber-300 border border-amber-300 dark:border-amber-800">
                                <Key className="w-2.5 h-2.5" /> PK
                              </span>
                            )}
                            {col.suggestedMasked && (
                              <span className="inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded text-[9px] bg-rose-100 dark:bg-rose-950 text-rose-700 dark:text-rose-300 border border-rose-200 dark:border-rose-800">
                                <ShieldAlert className="w-2.5 h-2.5" /> Sensible
                              </span>
                            )}
                          </div>
                        </td>
                        <td className="p-3 font-mono text-[11px] text-slate-500 dark:text-slate-400">
                          {col.sourceType}
                        </td>
                        <td className="p-3">
                          <select
                            id={`col-type-${col.name}`}
                            aria-label={`Type SmartObject pour ${col.name}`}
                            value={col.type}
                            disabled={!col.included}
                            onChange={(e) => updateColumnType(col.name, e.target.value)}
                            data-testid={`col-type-${col.name}`}
                            className="px-2 py-1 rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 font-mono text-[11px] text-blue-600 dark:text-blue-400 font-bold"
                          >
                            <option value="string">string</option>
                            <option value="number">number</option>
                            <option value="boolean">boolean</option>
                            <option value="datetime">datetime</option>
                            <option value="json">json</option>
                          </select>
                        </td>
                        <td className="p-3">
                          <label
                            htmlFor={`col-mask-${col.name}`}
                            className="inline-flex items-center gap-1.5 cursor-pointer text-[11px] font-medium text-slate-700 dark:text-slate-300"
                          >
                            <input
                              id={`col-mask-${col.name}`}
                              type="checkbox"
                              checked={col.masked}
                              disabled={!col.included}
                              onChange={() => toggleColumnMasked(col.name)}
                              data-testid={`col-mask-${col.name}`}
                              className="rounded border-slate-300 text-rose-600 focus:ring-rose-500 cursor-pointer"
                            />
                            <EyeOff className="w-3.5 h-3.5 text-slate-400" />
                            <span>{col.masked ? 'Masquée' : 'Visible'}</span>
                          </label>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </form>
          )}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-between px-6 py-4 border-t border-slate-200 dark:border-slate-700 bg-slate-50/60 dark:bg-slate-900/40">
          {step === 2 ? (
            <button
              type="button"
              onClick={() => setStep(1)}
              className="px-3.5 py-2 rounded-xl border border-slate-200 dark:border-slate-700 text-xs font-bold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 flex items-center gap-1 cursor-pointer"
            >
              <ChevronLeft className="w-3.5 h-3.5" />
              <span>Retour</span>
            </button>
          ) : (
            <button
              type="button"
              onClick={onClose}
              className="px-3.5 py-2 rounded-xl border border-slate-200 dark:border-slate-700 text-xs font-bold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
            >
              Annuler
            </button>
          )}

          {step === 1 ? (
            <button
              type="button"
              onClick={handleProceedToStep2}
              disabled={!selectedEntity}
              data-testid="import-next-step-btn"
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white rounded-xl text-xs font-bold flex items-center gap-1.5 shadow-sm cursor-pointer"
            >
              <span>Prévisualiser les colonnes</span>
              <ChevronRight className="w-3.5 h-3.5" />
            </button>
          ) : (
            <button
              type="submit"
              form="import-smartobject-form"
              disabled={isSubmitting || includedColumns.length === 0}
              data-testid="import-confirm-generate-btn"
              className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-xl text-xs font-bold flex items-center gap-1.5 shadow-sm cursor-pointer"
            >
              <CheckCircle2 className="w-3.5 h-3.5" />
              <span>
                {isSubmitting ? 'Génération...' : `Générer le SmartObject (${includedColumns.length} col.)`}
              </span>
            </button>
          )}
        </div>
      </div>
    </div>
  );
};
