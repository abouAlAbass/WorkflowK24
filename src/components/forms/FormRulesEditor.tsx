import React, { useMemo, useState } from 'react';
import { GitBranch, Plus, Trash2, Calculator, AlertTriangle } from 'lucide-react';
import type {
  FormRule,
  FormRuleEffect,
  JsonLogicValue,
  SmartFormField
} from '../../types/smartform';
import { analyzeFormRuleGraph } from '../../utils/jsonLogicEngine';

type RuleCombinator = 'and' | 'or';
type VisualOperator = '==' | '!=' | '>' | '>=' | '<' | '<=' | 'contains' | 'is_not_empty';

interface VisualClause {
  field: string;
  operator: VisualOperator;
  value: string;
}

interface ParsedCondition {
  combinator: RuleCombinator;
  clauses: VisualClause[];
}

function parseSingleClause(node: JsonLogicValue): VisualClause | null {
  if (!node || typeof node !== 'object' || Array.isArray(node)) return null;
  const keys = Object.keys(node);
  if (keys.length !== 1) return null;
  const op = keys[0];
  const args = (node as Record<string, JsonLogicValue>)[op];

  const extractVarName = (v: JsonLogicValue): string | null => {
    if (v && typeof v === 'object' && !Array.isArray(v) && 'var' in v) {
      const raw = (v as Record<string, JsonLogicValue>).var;
      if (typeof raw === 'string') return raw;
      if (Array.isArray(raw) && typeof raw[0] === 'string') return raw[0];
    }
    return null;
  };

  if (op === 'is_not_empty' || op === 'isNotEmpty' || op === '!!') {
    const arg = Array.isArray(args) ? args[0] : args;
    const fieldName = extractVarName(arg);
    if (fieldName) {
      return { field: fieldName, operator: 'is_not_empty', value: '' };
    }
    return null;
  }

  if (
    (op === '==' || op === '!=' || op === '>' || op === '>=' || op === '<' || op === '<=' || op === 'contains') &&
    Array.isArray(args) &&
    args.length >= 2
  ) {
    const fieldName = extractVarName(args[0]);
    if (fieldName) {
      return {
        field: fieldName,
        operator: op,
        value: args[1] === null || args[1] === undefined ? '' : String(args[1])
      };
    }
  }

  return null;
}

function parseConditionJsonLogic(
  condition: JsonLogicValue | null | undefined,
  defaultField: string
): ParsedCondition {
  if (!condition || typeof condition !== 'object' || Array.isArray(condition)) {
    return {
      combinator: 'and',
      clauses: [{ field: defaultField, operator: '==', value: '' }]
    };
  }

  const obj = condition as Record<string, JsonLogicValue>;
  if ('and' in obj && Array.isArray(obj.and)) {
    const parsed = obj.and.map(parseSingleClause).filter((c): c is VisualClause => c !== null);
    if (parsed.length > 0) return { combinator: 'and', clauses: parsed };
  }
  if ('or' in obj && Array.isArray(obj.or)) {
    const parsed = obj.or.map(parseSingleClause).filter((c): c is VisualClause => c !== null);
    if (parsed.length > 0) return { combinator: 'or', clauses: parsed };
  }

  const single = parseSingleClause(condition);
  if (single) {
    return { combinator: 'and', clauses: [single] };
  }

  return {
    combinator: 'and',
    clauses: [{ field: defaultField, operator: '==', value: '' }]
  };
}

function coerceLiteralValue(raw: string): string | number | boolean {
  const trimmed = raw.trim();
  if (trimmed === 'true') return true;
  if (trimmed === 'false') return false;
  if (trimmed !== '' && !Number.isNaN(Number(trimmed))) {
    return Number(trimmed);
  }
  return raw;
}

function serializeConditionJsonLogic(parsed: ParsedCondition): JsonLogicValue | null {
  if (parsed.clauses.length === 0) return null;

  const nodes: JsonLogicValue[] = parsed.clauses.map((c) => {
    if (c.operator === 'is_not_empty') {
      return { is_not_empty: [{ var: c.field }] };
    }
    return { [c.operator]: [{ var: c.field }, coerceLiteralValue(c.value)] };
  });

  if (nodes.length === 1) return nodes[0];
  return { [parsed.combinator]: nodes };
}

interface FormRulesEditorProps {
  selectedField: SmartFormField;
  allFields: SmartFormField[];
  allRules: FormRule[];
  disabled?: boolean;
  onAddRule: (rule: FormRule) => void;
  onUpdateRule: (ruleId: string, updates: Partial<FormRule>) => void;
  onRemoveRule: (ruleId: string) => void;
}

const EFFECT_LABELS: Record<FormRuleEffect, string> = {
  visible: 'Visible (Show when true)',
  required: 'Required (Mandatory when true)',
  readOnly: 'Read-Only (Lock when true)',
  computedValue: 'Computed Value (Formula)'
};

export const FormRulesEditor: React.FC<FormRulesEditorProps> = ({
  selectedField,
  allFields,
  allRules,
  disabled = false,
  onAddRule,
  onUpdateRule,
  onRemoveRule
}) => {
  const [formulaDrafts, setFormulaDrafts] = useState<Record<string, string>>({});
  const [formulaErrors, setFormulaErrors] = useState<Record<string, string>>({});

  const otherDataFields = useMemo(
    () => allFields.filter((f) => f.id !== selectedField.id && f.type !== 'section'),
    [allFields, selectedField.id]
  );

  const repeaterFields = useMemo(
    () => otherDataFields.filter((f) => f.type === 'repeater'),
    [otherDataFields]
  );

  const fieldRules = useMemo(
    () => allRules.filter((r) => r.targetField.toLowerCase() === selectedField.name.toLowerCase()),
    [allRules, selectedField.name]
  );

  const graphAnalysis = useMemo(
    () => analyzeFormRuleGraph(allFields, allRules),
    [allFields, allRules]
  );

  const defaultSourceField = otherDataFields[0]?.name || 'Amount';

  const inputClass =
    'w-full px-2.5 py-1.5 rounded-lg text-[11px] bg-white dark:bg-slate-800 border border-slate-300 dark:border-slate-700 text-[#0F172A] dark:text-slate-100 font-semibold focus:outline-none focus:ring-2 focus:ring-[#2563EB] disabled:opacity-60';

  const handleAddRule = () => {
    const newRule: FormRule = {
      id: `rule-${Date.now().toString(36)}`,
      targetField: selectedField.name,
      effect: 'visible',
      conditionJsonLogic: {
        '>': [{ var: defaultSourceField }, 0]
      },
      expressionJsonLogic: null
    };
    onAddRule(newRule);
  };

  const handleEffectChange = (rule: FormRule, nextEffect: FormRuleEffect) => {
    if (nextEffect === 'computedValue') {
      const defaultExpr: JsonLogicValue =
        repeaterFields.length > 0
          ? {
              sum: [
                { var: repeaterFields[0].name },
                repeaterFields[0].repeaterColumns?.find((c) => c.type === 'number')?.name || 'Amount'
              ]
            }
          : { '+': [{ var: defaultSourceField }, 0] };

      onUpdateRule(rule.id, {
        effect: nextEffect,
        conditionJsonLogic: null,
        expressionJsonLogic: rule.expressionJsonLogic ?? defaultExpr
      });
      setFormulaDrafts((prev) => ({
        ...prev,
        [rule.id]: JSON.stringify(rule.expressionJsonLogic ?? defaultExpr)
      }));
    } else {
      onUpdateRule(rule.id, {
        effect: nextEffect,
        conditionJsonLogic: rule.conditionJsonLogic ?? { '>': [{ var: defaultSourceField }, 0] },
        expressionJsonLogic: null
      });
    }
  };

  const updateCondition = (rule: FormRule, parsed: ParsedCondition) => {
    onUpdateRule(rule.id, {
      conditionJsonLogic: serializeConditionJsonLogic(parsed)
    });
  };

  const handleFormulaChange = (ruleId: string, rawJson: string) => {
    setFormulaDrafts((prev) => ({ ...prev, [ruleId]: rawJson }));
    try {
      const parsed = JSON.parse(rawJson) as JsonLogicValue;
      setFormulaErrors((prev) => {
        const copy = { ...prev };
        delete copy[ruleId];
        return copy;
      });
      onUpdateRule(ruleId, { expressionJsonLogic: parsed });
    } catch {
      setFormulaErrors((prev) => ({ ...prev, [ruleId]: 'Invalid JSONLogic syntax' }));
    }
  };

  const applyRepeaterSumPreset = (ruleId: string, repeater: SmartFormField, colName: string) => {
    const expr: JsonLogicValue = { sum: [{ var: repeater.name }, colName] };
    setFormulaDrafts((prev) => ({ ...prev, [ruleId]: JSON.stringify(expr) }));
    setFormulaErrors((prev) => {
      const copy = { ...prev };
      delete copy[ruleId];
      return copy;
    });
    onUpdateRule(ruleId, { expressionJsonLogic: expr });
  };

  return (
    <div
      data-testid="form-rules-editor"
      className="p-3 rounded-2xl border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-900/60 space-y-3"
    >
      <div className="flex items-center justify-between">
        <span className="text-[11px] font-extrabold flex items-center gap-1.5 text-[#0F172A] dark:text-white">
          <GitBranch className="w-3.5 h-3.5 text-purple-500" />
          <span>JSONLogic Rules ({fieldRules.length})</span>
        </span>
        {!disabled && (
          <button
            type="button"
            onClick={handleAddRule}
            data-testid="add-form-rule-btn"
            className="px-2 py-1 rounded-lg bg-purple-600 hover:bg-purple-700 text-white text-[10px] font-bold flex items-center gap-1 cursor-pointer"
          >
            <Plus className="w-3 h-3" />
            <span>Add Rule</span>
          </button>
        )}
      </div>

      {graphAnalysis.hasCycle && (
        <div
          role="alert"
          data-testid="rule-cycle-warning"
          className="p-2 rounded-xl border border-rose-300 dark:border-rose-800 bg-rose-50 dark:bg-rose-950/40 text-[10px] font-bold text-rose-700 dark:text-rose-300 flex items-start gap-1.5"
        >
          <AlertTriangle className="w-3.5 h-3.5 shrink-0 mt-0.5" />
          <span>{`Cycle detected: ${graphAnalysis.cyclePath.join(' -> ')}`}</span>
        </div>
      )}

      {fieldRules.length === 0 ? (
        <p className="text-[10px] text-[#64748B] dark:text-slate-400">
          Add declarative rules to control visibility, conditional required, read-only state, or calculated formulas.
        </p>
      ) : (
        <div className="space-y-2.5">
          {fieldRules.map((rule) => {
            const parsedCond = parseConditionJsonLogic(rule.conditionJsonLogic, defaultSourceField);
            const rawFormula =
              formulaDrafts[rule.id] ??
              (rule.expressionJsonLogic ? JSON.stringify(rule.expressionJsonLogic) : '');

            return (
              <div
                key={rule.id}
                data-testid={`rule-card-${rule.id}`}
                className="p-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 space-y-2"
              >
                <div className="flex items-center justify-between gap-1.5">
                  <label htmlFor={`rule-effect-${rule.id}`} className="sr-only">
                    Rule Effect
                  </label>
                  <select
                    id={`rule-effect-${rule.id}`}
                    disabled={disabled}
                    value={rule.effect}
                    onChange={(e) => handleEffectChange(rule, e.target.value as FormRuleEffect)}
                    className={inputClass}
                  >
                    {(Object.keys(EFFECT_LABELS) as FormRuleEffect[]).map((eff) => (
                      <option key={eff} value={eff}>
                        {EFFECT_LABELS[eff]}
                      </option>
                    ))}
                  </select>
                  {!disabled && (
                    <button
                      type="button"
                      onClick={() => onRemoveRule(rule.id)}
                      aria-label="Delete rule"
                      className="p-1 text-slate-400 hover:text-rose-500 cursor-pointer shrink-0"
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
                  )}
                </div>

                {rule.effect !== 'computedValue' && (
                  <div className="space-y-1.5">
                    {parsedCond.clauses.length > 1 && (
                      <div className="flex items-center gap-1.5">
                        <span className="text-[10px] font-bold text-slate-500">Match:</span>
                        <select
                          aria-label="Condition combinator"
                          disabled={disabled}
                          value={parsedCond.combinator}
                          onChange={(e) =>
                            updateCondition(rule, {
                              ...parsedCond,
                              combinator: e.target.value as RuleCombinator
                            })
                          }
                          className="px-2 py-0.5 rounded-md text-[10px] font-bold bg-purple-500/10 text-purple-700 dark:text-purple-300 border border-purple-200 dark:border-purple-800"
                        >
                          <option value="and">ALL (AND)</option>
                          <option value="or">ANY (OR)</option>
                        </select>
                      </div>
                    )}

                    {parsedCond.clauses.map((clause, cIdx) => (
                      <div key={cIdx} className="space-y-1 p-2 rounded-lg bg-slate-50 dark:bg-slate-800/60">
                        <div className="flex items-center gap-1">
                          <select
                            aria-label={`Condition ${cIdx + 1} field`}
                            disabled={disabled}
                            value={clause.field}
                            onChange={(e) => {
                              const nextClauses = parsedCond.clauses.map((cl, idx) =>
                                idx === cIdx ? { ...cl, field: e.target.value } : cl
                              );
                              updateCondition(rule, { ...parsedCond, clauses: nextClauses });
                            }}
                            className={inputClass}
                          >
                            {otherDataFields.map((f) => (
                              <option key={f.id} value={f.name}>
                                {f.label} ({f.name})
                              </option>
                            ))}
                          </select>
                          {parsedCond.clauses.length > 1 && !disabled && (
                            <button
                              type="button"
                              onClick={() => {
                                const nextClauses = parsedCond.clauses.filter((_, idx) => idx !== cIdx);
                                updateCondition(rule, { ...parsedCond, clauses: nextClauses });
                              }}
                              aria-label="Remove condition"
                              className="p-1 text-slate-400 hover:text-rose-500 cursor-pointer"
                            >
                              <Trash2 className="w-3 h-3" />
                            </button>
                          )}
                        </div>

                        <div className="grid grid-cols-2 gap-1">
                          <select
                            aria-label={`Condition ${cIdx + 1} operator`}
                            disabled={disabled}
                            value={clause.operator}
                            onChange={(e) => {
                              const nextClauses = parsedCond.clauses.map((cl, idx) =>
                                idx === cIdx ? { ...cl, operator: e.target.value as VisualOperator } : cl
                              );
                              updateCondition(rule, { ...parsedCond, clauses: nextClauses });
                            }}
                            className={inputClass}
                          >
                            <option value="==">Equals (==)</option>
                            <option value="!=">Not Equals (!=)</option>
                            <option value=">">Greater (&gt;)</option>
                            <option value=">=">Greater/Eq (&gt;=)</option>
                            <option value="<">Less (&lt;)</option>
                            <option value="<=">Less/Eq (&lt;=)</option>
                            <option value="contains">Contains</option>
                            <option value="is_not_empty">Is Not Empty</option>
                          </select>

                          {clause.operator !== 'is_not_empty' && (
                            <input
                              type="text"
                              aria-label={`Condition ${cIdx + 1} value`}
                              disabled={disabled}
                              value={clause.value}
                              onChange={(e) => {
                                const nextClauses = parsedCond.clauses.map((cl, idx) =>
                                  idx === cIdx ? { ...cl, value: e.target.value } : cl
                                );
                                updateCondition(rule, { ...parsedCond, clauses: nextClauses });
                              }}
                              placeholder="Value"
                              className={`${inputClass} font-mono`}
                            />
                          )}
                        </div>
                      </div>
                    ))}

                    {!disabled && (
                      <button
                        type="button"
                        onClick={() =>
                          updateCondition(rule, {
                            ...parsedCond,
                            clauses: [
                              ...parsedCond.clauses,
                              { field: defaultSourceField, operator: '==', value: '' }
                            ]
                          })
                        }
                        className="text-[10px] font-bold text-purple-600 dark:text-purple-400 hover:underline flex items-center gap-1 cursor-pointer"
                      >
                        <Plus className="w-3 h-3" />
                        <span>Add Condition (AND / OR)</span>
                      </button>
                    )}
                  </div>
                )}

                {rule.effect === 'computedValue' && (
                  <div className="space-y-1.5">
                    <div className="flex items-center gap-1 text-[10px] font-bold text-emerald-700 dark:text-emerald-400">
                      <Calculator className="w-3 h-3" />
                      <span>JSONLogic Formula Expression</span>
                    </div>

                    {repeaterFields.length > 0 && !disabled && (
                      <div className="flex flex-wrap gap-1">
                        {repeaterFields.flatMap((rep) =>
                          (rep.repeaterColumns || [])
                            .filter((c) => c.type === 'number')
                            .map((col) => (
                              <button
                                key={`${rep.name}.${col.name}`}
                                type="button"
                                onClick={() => applyRepeaterSumPreset(rule.id, rep, col.name)}
                                className="px-2 py-0.5 rounded-md bg-emerald-500/10 hover:bg-emerald-500/20 text-emerald-700 dark:text-emerald-300 text-[10px] font-mono font-bold cursor-pointer"
                              >
                                {`sum(${rep.name}.${col.name})`}
                              </button>
                            ))
                        )}
                      </div>
                    )}

                    <label htmlFor={`rule-expr-${rule.id}`} className="sr-only">
                      Formula JSONLogic
                    </label>
                    <textarea
                      id={`rule-expr-${rule.id}`}
                      rows={3}
                      disabled={disabled}
                      value={rawFormula}
                      onChange={(e) => handleFormulaChange(rule.id, e.target.value)}
                      placeholder='{"sum": [{"var": "LineItems"}, "Amount"]}'
                      aria-invalid={Boolean(formulaErrors[rule.id])}
                      className={`${inputClass} font-mono text-[10px]`}
                    />
                    {formulaErrors[rule.id] && (
                      <p role="alert" className="text-[10px] font-bold text-rose-600 dark:text-rose-400">
                        {formulaErrors[rule.id]}
                      </p>
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
