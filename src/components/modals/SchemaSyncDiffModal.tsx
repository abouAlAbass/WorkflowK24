import React, { useEffect, useState } from 'react';
import { RefreshCw, X, AlertTriangle, CheckCircle2, Plus, Minus, ArrowRight } from 'lucide-react';
import { getWorkflowRepository } from '../../services/repositoryManager';
import type { SchemaDiffResult, SmartObjectEntity } from '../../types/workflow';

export interface SchemaSyncDiffModalProps {
  isOpen: boolean;
  smartObject: SmartObjectEntity | null;
  onClose: () => void;
  onApplied: (diff: SchemaDiffResult) => void;
}

export const SchemaSyncDiffModal: React.FC<SchemaSyncDiffModalProps> = ({
  isOpen,
  smartObject,
  onClose,
  onApplied
}) => {
  const [diff, setDiff] = useState<SchemaDiffResult | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isApplying, setIsApplying] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isOpen || !smartObject) return;
    let cancelled = false;
    setDiff(null);
    setError(null);
    setIsLoading(true);

    getWorkflowRepository()
      .syncSmartObjectSchema(smartObject.id, false)
      .then((res) => {
        if (!cancelled) setDiff(res);
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Impossible de calculer le diff de schéma.');
        }
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [isOpen, smartObject]);

  const handleApply = async () => {
    if (!smartObject) return;
    setError(null);
    setIsApplying(true);
    try {
      const appliedDiff = await getWorkflowRepository().syncSmartObjectSchema(smartObject.id, true);
      onApplied(appliedDiff);
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Échec de l’application du diff de schéma.');
    } finally {
      setIsApplying(false);
    }
  };

  if (!isOpen || !smartObject) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="schema-sync-diff-title"
      data-testid="schema-sync-diff-modal"
    >
      <div className="bg-white dark:bg-[#1E293B] border border-slate-200 dark:border-slate-700 rounded-3xl max-w-2xl w-full p-6 shadow-2xl space-y-5">
        <div className="flex items-center justify-between pb-3 border-b border-slate-200 dark:border-slate-700">
          <div className="flex items-center gap-2">
            <RefreshCw className="w-5 h-5 text-blue-600" />
            <div>
              <h3 id="schema-sync-diff-title" className="text-sm font-extrabold text-slate-900 dark:text-white">
                Actualiser depuis la source — {smartObject.displayName}
              </h3>
              <p className="text-[11px] font-mono text-slate-500">
                Source : {smartObject.sourceEntityName || smartObject.name}
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Fermer"
            className="p-1 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 rounded-lg cursor-pointer"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {error && (
          <div
            role="alert"
            className="p-3 rounded-xl bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900 text-xs text-red-700 dark:text-red-300 flex items-center gap-2"
          >
            <AlertTriangle className="w-4 h-4 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        {isLoading && (
          <div className="py-8 text-center text-xs text-slate-500 flex flex-col items-center gap-2">
            <RefreshCw className="w-5 h-5 animate-spin text-blue-600" />
            <span>Analyse du schéma distant en cours...</span>
          </div>
        )}

        {!isLoading && diff && (
          <div className="space-y-3 max-h-96 overflow-y-auto">
            {!diff.hasChanges ? (
              <div className="p-4 rounded-2xl bg-emerald-50 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-900 text-xs text-emerald-700 dark:text-emerald-300 flex items-center gap-2">
                <CheckCircle2 className="w-4 h-4 shrink-0" />
                <span>Le SmartObject est déjà synchronisé avec le schéma source.</span>
              </div>
            ) : (
              <>
                {diff.primaryKeyChanged && (
                  <div className="p-3 rounded-xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800 text-xs text-amber-800 dark:text-amber-300">
                    Clé primaire modifiée :{' '}
                    <span className="font-mono font-bold">{diff.previousPrimaryKey || '(aucune)'}</span>{' '}
                    → <span className="font-mono font-bold">{diff.newPrimaryKey || '(aucune)'}</span>
                  </div>
                )}

                {diff.addedFields.length > 0 && (
                  <div className="space-y-1.5">
                    <span className="text-[11px] font-bold text-emerald-700 dark:text-emerald-400">
                      + Colonnes ajoutées ({diff.addedFields.length})
                    </span>
                    <ul className="space-y-1">
                      {diff.addedFields.map((f) => (
                        <li
                          key={f.name}
                          className="px-3 py-1.5 rounded-xl bg-emerald-50/70 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-900 text-xs font-mono flex items-center justify-between"
                        >
                          <span className="flex items-center gap-1.5 text-emerald-800 dark:text-emerald-300 font-bold">
                            <Plus className="w-3.5 h-3.5" /> {f.name}
                          </span>
                          <span className="text-[11px] text-emerald-600 dark:text-emerald-400">
                            {f.mappedSmartObjectType} ({f.sourceType})
                          </span>
                        </li>
                      ))}
                    </ul>
                  </div>
                )}

                {diff.removedFields.length > 0 && (
                  <div className="space-y-1.5">
                    <span className="text-[11px] font-bold text-rose-700 dark:text-rose-400">
                      - Colonnes supprimées ({diff.removedFields.length})
                    </span>
                    <ul className="space-y-1">
                      {diff.removedFields.map((f) => (
                        <li
                          key={f.name}
                          className="px-3 py-1.5 rounded-xl bg-rose-50/70 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-900 text-xs font-mono flex items-center justify-between"
                        >
                          <span className="flex items-center gap-1.5 text-rose-800 dark:text-rose-300 font-bold">
                            <Minus className="w-3.5 h-3.5" /> {f.name}
                          </span>
                          <span className="text-[11px] text-rose-600 dark:text-rose-400">{f.type}</span>
                        </li>
                      ))}
                    </ul>
                  </div>
                )}

                {diff.typeChangedFields.length > 0 && (
                  <div className="space-y-1.5">
                    <span className="text-[11px] font-bold text-amber-700 dark:text-amber-400">
                      ~ Colonnes modifiées ({diff.typeChangedFields.length})
                    </span>
                    <ul className="space-y-1">
                      {diff.typeChangedFields.map((f) => (
                        <li
                          key={f.name}
                          className="px-3 py-1.5 rounded-xl bg-amber-50/70 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-900 text-xs font-mono flex items-center justify-between"
                        >
                          <span className="text-amber-900 dark:text-amber-200 font-bold">~ {f.name}</span>
                          <span className="flex items-center gap-1.5 text-[11px] text-amber-700 dark:text-amber-300">
                            {f.previousType} <ArrowRight className="w-3 h-3" /> {f.newType}
                          </span>
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
              </>
            )}
          </div>
        )}

        <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-200 dark:border-slate-700">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 rounded-xl border border-slate-200 dark:border-slate-700 text-xs font-bold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
          >
            Fermer
          </button>
          {diff?.hasChanges && (
            <button
              type="button"
              onClick={handleApply}
              disabled={isApplying}
              data-testid="confirm-apply-schema-diff-btn"
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white rounded-xl text-xs font-bold shadow-md cursor-pointer transition-all"
            >
              {isApplying ? 'Application...' : 'Confirmer et appliquer le diff'}
            </button>
          )}
        </div>
      </div>
    </div>
  );
};
