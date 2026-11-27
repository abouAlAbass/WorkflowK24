import type {
  FormRule,
  JsonLogicValue,
  SmartFormField,
  SmartFormVisibilityRule
} from '../types/smartform';

const MAX_DEPTH = 64;

export function isTruthy(value: unknown): boolean {
  if (value === null || value === undefined) return false;
  if (typeof value === 'boolean') return value;
  if (typeof value === 'string') return value.length > 0;
  if (typeof value === 'number') return !Number.isNaN(value) && value !== 0;
  if (Array.isArray(value)) return value.length > 0;
  return true;
}

function tryToNumber(value: unknown): number | null {
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : null;
  }
  if (typeof value === 'string') {
    const trimmed = value.trim();
    if (trimmed === '') return null;
    const parsed = Number(trimmed);
    return Number.isFinite(parsed) ? parsed : null;
  }
  return null;
}

function toNumberOrZero(value: unknown): number {
  return tryToNumber(value) ?? 0;
}

function normalizeNumber(value: number): number {
  if (!Number.isFinite(value)) return 0;
  const rounded = Math.round(value * 1e6) / 1e6;
  return Object.is(rounded, -0) ? 0 : rounded;
}

function toComparableString(value: unknown): string {
  if (value === null || value === undefined) return '';
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  return String(value);
}

function resolvePath(current: unknown, path: string): { found: boolean; value: unknown } {
  if (current === null || current === undefined) return { found: false, value: undefined };
  const segments = path.split('.');
  let cursor: unknown = current;

  for (const seg of segments) {
    if (cursor === null || cursor === undefined) {
      return { found: false, value: undefined };
    }

    if (Array.isArray(cursor)) {
      const idx = Number(seg);
      if (!Number.isInteger(idx) || idx < 0 || idx >= cursor.length) {
        return { found: false, value: undefined };
      }
      cursor = cursor[idx];
    } else if (typeof cursor === 'object') {
      const record = cursor as Record<string, unknown>;
      if (seg in record) {
        cursor = record[seg];
      } else {
        const lower = seg.toLowerCase();
        const matchKey = Object.keys(record).find((k) => k.toLowerCase() === lower);
        if (!matchKey) {
          return { found: false, value: undefined };
        }
        cursor = record[matchKey];
      }
    } else {
      return { found: false, value: undefined };
    }
  }

  return { found: true, value: cursor };
}

function buildRowScope(parentData: Record<string, unknown>, row: unknown): Record<string, unknown> {
  if (row && typeof row === 'object' && !Array.isArray(row)) {
    return { ...parentData, ...(row as Record<string, unknown>) };
  }
  return { ...parentData, '': row };
}

function looseEquals(left: unknown, right: unknown): boolean {
  if (left === null || left === undefined) {
    return toComparableString(right).length === 0;
  }
  if (right === null || right === undefined) {
    return toComparableString(left).length === 0;
  }
  if (typeof left === 'boolean' && typeof right === 'boolean') {
    return left === right;
  }
  const lNum = tryToNumber(left);
  const rNum = tryToNumber(right);
  if (lNum !== null && rNum !== null) {
    return lNum === rNum;
  }
  return toComparableString(left).toLowerCase() === toComparableString(right).toLowerCase();
}

function strictEquals(left: unknown, right: unknown): boolean {
  if (left === right) return true;
  if (typeof left === 'number' && typeof right === 'number') {
    return left === right;
  }
  return false;
}

function comparePair(
  left: unknown,
  right: unknown,
  numCmp: (a: number, b: number) => boolean,
  strCmp: (a: string, b: string) => boolean
): boolean {
  const lNum = tryToNumber(left);
  const rNum = tryToNumber(right);
  if (lNum !== null && rNum !== null) {
    return numCmp(lNum, rNum);
  }
  return strCmp(toComparableString(left), toComparableString(right));
}

function evaluateComparison(
  args: unknown[],
  numCmp: (a: number, b: number) => boolean,
  strCmp: (a: string, b: string) => boolean
): boolean {
  if (args.length < 2) return false;
  for (let i = 0; i < args.length - 1; i++) {
    if (!comparePair(args[i], args[i + 1], numCmp, strCmp)) {
      return false;
    }
  }
  return true;
}

function evalNode(node: JsonLogicValue | null | undefined, data: Record<string, unknown>, depth: number): unknown {
  if (depth > MAX_DEPTH) {
    throw new Error('JSONLogic expression exceeded maximum nesting depth.');
  }

  if (node === null || node === undefined) return null;
  if (typeof node !== 'object') return node;
  if (Array.isArray(node)) {
    return node.map((item) => evalNode(item, data, depth + 1));
  }

  const keys = Object.keys(node);
  if (keys.length === 0) return null;

  const op = keys[0];
  const rawArgs = (node as Record<string, JsonLogicValue>)[op];

  // Lazy / special operators
  if (op === 'var') {
    let path: string | null = null;
    let defaultVal: unknown = null;
    if (Array.isArray(rawArgs)) {
      if (rawArgs.length === 0) return data;
      path = toComparableString(evalNode(rawArgs[0], data, depth + 1));
      if (rawArgs.length > 1) {
        defaultVal = evalNode(rawArgs[1], data, depth + 1);
      }
    } else {
      const evaluated = evalNode(rawArgs, data, depth + 1);
      if (evaluated === null || evaluated === undefined) return data;
      path = toComparableString(evaluated);
    }

    if (!path) return data;
    const res = resolvePath(data, path);
    return res.found && res.value !== undefined && res.value !== null ? res.value : defaultVal;
  }

  if (op === 'and') {
    if (!Array.isArray(rawArgs)) return evalNode(rawArgs, data, depth + 1);
    let last: unknown = true;
    for (const item of rawArgs) {
      last = evalNode(item, data, depth + 1);
      if (!isTruthy(last)) return last;
    }
    return last;
  }

  if (op === 'or') {
    if (!Array.isArray(rawArgs)) return evalNode(rawArgs, data, depth + 1);
    let last: unknown = false;
    for (const item of rawArgs) {
      last = evalNode(item, data, depth + 1);
      if (isTruthy(last)) return last;
    }
    return last;
  }

  if (op === 'if' || op === '?:') {
    if (!Array.isArray(rawArgs)) return evalNode(rawArgs, data, depth + 1);
    let i = 0;
    while (i + 1 < rawArgs.length) {
      if (isTruthy(evalNode(rawArgs[i], data, depth + 1))) {
        return evalNode(rawArgs[i + 1], data, depth + 1);
      }
      i += 2;
    }
    return i < rawArgs.length ? evalNode(rawArgs[i], data, depth + 1) : null;
  }

  if (op === 'sum' || op === 'avg') {
    const isAvg = op === 'avg';
    if (Array.isArray(rawArgs)) {
      if (rawArgs.length === 2) {
        const firstEval = evalNode(rawArgs[0], data, depth + 1);
        if (Array.isArray(firstEval)) {
          if (firstEval.length === 0) return 0;
          let total = 0;
          const selector = rawArgs[1];
          for (const row of firstEval) {
            let val: unknown;
            if (typeof selector === 'string') {
              const resolved = resolvePath(row, selector);
              val = resolved.found ? resolved.value : 0;
            } else {
              val = evalNode(selector, buildRowScope(data, row), depth + 1);
            }
            total += toNumberOrZero(val);
          }
          return normalizeNumber(isAvg ? total / firstEval.length : total);
        }
      }

      const nums: number[] = [];
      for (const item of rawArgs) {
        const evaluated = evalNode(item, data, depth + 1);
        if (Array.isArray(evaluated)) {
          for (const sub of evaluated) nums.push(toNumberOrZero(sub));
        } else {
          nums.push(toNumberOrZero(evaluated));
        }
      }
      if (nums.length === 0) return 0;
      const sum = nums.reduce((acc, n) => acc + n, 0);
      return normalizeNumber(isAvg ? sum / nums.length : sum);
    }

    const single = evalNode(rawArgs, data, depth + 1);
    if (Array.isArray(single)) {
      if (single.length === 0) return 0;
      const sum = single.reduce((acc: number, n) => acc + toNumberOrZero(n), 0);
      return normalizeNumber(isAvg ? sum / single.length : sum);
    }
    return normalizeNumber(toNumberOrZero(single));
  }

  if (op === 'count') {
    const args = Array.isArray(rawArgs)
      ? rawArgs.map((a) => evalNode(a, data, depth + 1))
      : [evalNode(rawArgs, data, depth + 1)];
    if (args.length === 0 || args[0] === null || args[0] === undefined) return 0;
    return Array.isArray(args[0]) ? args[0].length : 1;
  }

  if (op === 'map' && Array.isArray(rawArgs) && rawArgs.length >= 2) {
    const source = evalNode(rawArgs[0], data, depth + 1);
    if (!Array.isArray(source)) return [];
    return source.map((item) => evalNode(rawArgs[1], buildRowScope(data, item), depth + 1));
  }

  if (op === 'filter' && Array.isArray(rawArgs) && rawArgs.length >= 2) {
    const source = evalNode(rawArgs[0], data, depth + 1);
    if (!Array.isArray(source)) return [];
    return source.filter((item) => isTruthy(evalNode(rawArgs[1], buildRowScope(data, item), depth + 1)));
  }

  if (op === 'reduce' && Array.isArray(rawArgs) && rawArgs.length >= 3) {
    const source = evalNode(rawArgs[0], data, depth + 1);
    let acc = evalNode(rawArgs[2], data, depth + 1);
    if (!Array.isArray(source)) return acc;
    for (const item of source) {
      acc = evalNode(rawArgs[1], { current: item, accumulator: acc }, depth + 1);
    }
    return acc;
  }

  // Eager operators
  const args: unknown[] = Array.isArray(rawArgs)
    ? rawArgs.map((a) => evalNode(a, data, depth + 1))
    : [evalNode(rawArgs, data, depth + 1)];

  switch (op) {
    case '!':
      return !isTruthy(args[0]);
    case '!!':
      return isTruthy(args[0]);
    case '==':
      return args.length >= 2 && looseEquals(args[0], args[1]);
    case '!=':
      return args.length >= 2 && !looseEquals(args[0], args[1]);
    case '===':
      return args.length >= 2 && strictEquals(args[0], args[1]);
    case '!==':
      return args.length >= 2 && !strictEquals(args[0], args[1]);
    case '>':
      return evaluateComparison(args, (a, b) => a > b, (a, b) => a > b);
    case '>=':
      return evaluateComparison(args, (a, b) => a >= b, (a, b) => a >= b);
    case '<':
      return evaluateComparison(args, (a, b) => a < b, (a, b) => a < b);
    case '<=':
      return evaluateComparison(args, (a, b) => a <= b, (a, b) => a <= b);
    case '+':
      return normalizeNumber(args.reduce<number>((sum, v) => sum + toNumberOrZero(v), 0));
    case '-':
      if (args.length === 0) return 0;
      if (args.length === 1) return normalizeNumber(-toNumberOrZero(args[0]));
      return normalizeNumber(
        args.slice(1).reduce<number>((acc, v) => acc - toNumberOrZero(v), toNumberOrZero(args[0]))
      );
    case '*':
      if (args.length === 0) return 0;
      return normalizeNumber(args.reduce<number>((prod, v) => prod * toNumberOrZero(v), 1));
    case '/': {
      if (args.length < 2) return 0;
      const divisor = toNumberOrZero(args[1]);
      return divisor === 0 ? 0 : normalizeNumber(toNumberOrZero(args[0]) / divisor);
    }
    case '%': {
      if (args.length < 2) return 0;
      const divisor = toNumberOrZero(args[1]);
      return divisor === 0 ? 0 : normalizeNumber(toNumberOrZero(args[0]) % divisor);
    }
    case 'min':
      if (args.length === 0) return 0;
      return normalizeNumber(Math.min(...args.map(toNumberOrZero)));
    case 'max':
      if (args.length === 0) return 0;
      return normalizeNumber(Math.max(...args.map(toNumberOrZero)));
    case 'round': {
      if (args.length === 0) return 0;
      const val = toNumberOrZero(args[0]);
      const decimals = args.length > 1 ? Math.max(0, Math.min(10, Math.trunc(toNumberOrZero(args[1])))) : 0;
      const factor = Math.pow(10, decimals);
      return normalizeNumber(Math.round(val * factor) / factor);
    }
    case 'in': {
      if (args.length < 2) return false;
      const [needle, haystack] = args;
      if (typeof haystack === 'string') {
        return haystack.toLowerCase().includes(toComparableString(needle).toLowerCase());
      }
      if (Array.isArray(haystack)) {
        return haystack.some((item) => looseEquals(item, needle));
      }
      return false;
    }
    case 'contains': {
      if (args.length < 2) return false;
      const [haystack, needle] = args;
      if (Array.isArray(haystack)) {
        return haystack.some((item) => looseEquals(item, needle));
      }
      return toComparableString(haystack).toLowerCase().includes(toComparableString(needle).toLowerCase());
    }
    case 'cat':
      return args.map(toComparableString).join('');
    case 'is_not_empty':
    case 'isNotEmpty':
      return args.length > 0 && toComparableString(args[0]).trim().length > 0;
    default:
      return null;
  }
}

export function evaluateJsonLogic(
  rule: JsonLogicValue | null | undefined,
  data: Record<string, unknown>
): unknown {
  return evalNode(rule, data, 0);
}

export function evaluateJsonLogicBool(
  rule: JsonLogicValue | null | undefined,
  data: Record<string, unknown>
): boolean {
  return isTruthy(evalNode(rule, data, 0));
}

export interface FormRuleGraphAnalysis {
  hasCycle: boolean;
  cyclePath: string[];
  topologicalOrder: string[];
}

function extractVarRefs(
  node: JsonLogicValue | null | undefined,
  refs: Set<string>,
  repeaterColsByField: Map<string, Set<string>>,
  excludedRowCols?: Set<string>
): void {
  if (node === null || node === undefined || typeof node !== 'object') return;
  if (Array.isArray(node)) {
    for (const item of node) extractVarRefs(item, refs, repeaterColsByField, excludedRowCols);
    return;
  }

  for (const [op, rawVal] of Object.entries(node)) {
    if (op === 'var') {
      let rawPath: string | null = null;
      if (typeof rawVal === 'string') {
        rawPath = rawVal;
      } else if (Array.isArray(rawVal) && typeof rawVal[0] === 'string') {
        rawPath = rawVal[0];
      }
      if (rawPath) {
        const root = rawPath.split('.')[0].trim();
        if (root && (!excludedRowCols || !excludedRowCols.has(root.toLowerCase()))) {
          refs.add(root);
        }
      }
    } else if ((op === 'sum' || op === 'avg' || op === 'map' || op === 'filter') && Array.isArray(rawVal)) {
      if (rawVal.length >= 1) {
        const sourceRefs = new Set<string>();
        extractVarRefs(rawVal[0], sourceRefs, repeaterColsByField, excludedRowCols);
        for (const r of sourceRefs) refs.add(r);

        const nextExcluded = new Set<string>(excludedRowCols ?? []);
        for (const src of sourceRefs) {
          const cols = repeaterColsByField.get(src.toLowerCase());
          if (cols) {
            for (const c of cols) nextExcluded.add(c);
          }
        }

        for (let i = 1; i < rawVal.length; i++) {
          if (typeof rawVal[i] !== 'string') {
            extractVarRefs(rawVal[i], refs, repeaterColsByField, nextExcluded);
          }
        }
      }
    } else {
      extractVarRefs(rawVal, refs, repeaterColsByField, excludedRowCols);
    }
  }
}

export function analyzeFormRuleGraph(
  fields: SmartFormField[],
  rules?: FormRule[] | null
): FormRuleGraphAnalysis {
  const canonicalByLower = new Map<string, string>();
  const orderedNodes: string[] = [];
  const repeaterColsByField = new Map<string, Set<string>>();
  const adjacency = new Map<string, Set<string>>();

  const ensureNode = (name: string) => {
    if (!name) return;
    const lower = name.toLowerCase();
    if (!canonicalByLower.has(lower)) {
      canonicalByLower.set(lower, name);
      orderedNodes.push(name);
      adjacency.set(name, new Set());
    }
  };

  for (const field of fields) {
    if (!field.name) continue;
    ensureNode(field.name);
    if (field.type === 'repeater' && field.repeaterColumns?.length) {
      repeaterColsByField.set(
        field.name.toLowerCase(),
        new Set(field.repeaterColumns.map((c) => c.name.toLowerCase()))
      );
    }
  }

  const addDep = (target: string, source: string) => {
    if (!target || !source) return;
    ensureNode(target);
    ensureNode(source);
    const canonSrc = canonicalByLower.get(source.toLowerCase())!;
    const canonTgt = canonicalByLower.get(target.toLowerCase())!;
    adjacency.get(canonSrc)!.add(canonTgt);
  };

  for (const field of fields) {
    if (field.visibilityRule?.field) {
      addDep(field.name, field.visibilityRule.field);
    }
  }

  if (rules) {
    for (const rule of rules) {
      if (!rule.targetField) continue;
      ensureNode(rule.targetField);
      const refs = new Set<string>();
      extractVarRefs(rule.conditionJsonLogic, refs, repeaterColsByField);
      extractVarRefs(rule.expressionJsonLogic, refs, repeaterColsByField);
      for (const src of refs) {
        addDep(rule.targetField, src);
      }
    }
  }

  const color = new Map<string, number>(); // 0 unvisited, 1 visiting, 2 visited
  const parent = new Map<string, string>();

  const dfs = (node: string): string[] | null => {
    color.set(node, 1);
    const neighbors = adjacency.get(node);
    if (neighbors) {
      for (const next of neighbors) {
        const st = color.get(next) ?? 0;
        if (st === 1) {
          const cycle: string[] = [next];
          let curr = node;
          while (curr.toLowerCase() !== next.toLowerCase()) {
            cycle.push(curr);
            const p = parent.get(curr);
            if (!p) break;
            curr = p;
          }
          cycle.push(next);
          cycle.reverse();
          return cycle;
        }
        if (st === 0) {
          parent.set(next, node);
          const nested = dfs(next);
          if (nested) return nested;
        }
      }
    }
    color.set(node, 2);
    return null;
  };

  for (const node of orderedNodes) {
    if ((color.get(node) ?? 0) !== 0) continue;
    const cycle = dfs(node);
    if (cycle) {
      return { hasCycle: true, cyclePath: cycle, topologicalOrder: [] };
    }
  }

  const inDegree = new Map<string, number>();
  for (const node of orderedNodes) inDegree.set(node, 0);
  for (const targets of adjacency.values()) {
    for (const t of targets) {
      inDegree.set(t, (inDegree.get(t) ?? 0) + 1);
    }
  }

  const queue = orderedNodes.filter((n) => (inDegree.get(n) ?? 0) === 0);
  const topo: string[] = [];
  while (queue.length > 0) {
    const curr = queue.shift()!;
    topo.push(curr);
    for (const next of adjacency.get(curr) ?? []) {
      const deg = (inDegree.get(next) ?? 1) - 1;
      inDegree.set(next, deg);
      if (deg === 0) queue.push(next);
    }
  }

  return { hasCycle: false, cyclePath: [], topologicalOrder: topo };
}

export function evaluateLegacyVisibilityRule(
  rule: SmartFormVisibilityRule | null | undefined,
  values: Record<string, unknown>
): boolean {
  if (!rule || !rule.field) return true;

  const rawActual = values[rule.field];
  const actualStr = rawActual === undefined || rawActual === null ? '' : String(rawActual);
  const expectedStr = rule.value ?? '';

  if (rule.operator === 'is_not_empty') {
    return actualStr.trim().length > 0;
  }

  const actualNum = Number(actualStr);
  const expectedNum = Number(expectedStr);
  const bothNumeric =
    actualStr.trim() !== '' &&
    expectedStr.trim() !== '' &&
    !Number.isNaN(actualNum) &&
    !Number.isNaN(expectedNum);

  switch (rule.operator) {
    case 'greater_than':
      return bothNumeric ? actualNum > expectedNum : actualStr > expectedStr;
    case 'less_than':
      return bothNumeric ? actualNum < expectedNum : actualStr < expectedStr;
    case 'equals':
      return bothNumeric ? actualNum === expectedNum : actualStr.toLowerCase() === expectedStr.toLowerCase();
    case 'not_equals':
      return bothNumeric ? actualNum !== expectedNum : actualStr.toLowerCase() !== expectedStr.toLowerCase();
    case 'contains':
      return actualStr.toLowerCase().includes(expectedStr.toLowerCase());
    default:
      return true;
  }
}

export interface SmartFormRuleEvalState {
  effectiveValues: Record<string, unknown>;
  visibleFields: Set<string>;
  requiredFields: Set<string>;
  readOnlyFields: Set<string>;
  computedFields: Set<string>;
  hasCycle: boolean;
  cyclePath: string[];
}

export function evaluateSmartFormState(
  fields: SmartFormField[],
  rules: FormRule[] | null | undefined,
  rawValues: Record<string, unknown>
): SmartFormRuleEvalState {
  const dataFields = fields.filter((f) => f.type !== 'section');
  const fieldByLower = new Map<string, SmartFormField>();
  for (const f of dataFields) {
    fieldByLower.set(f.name.toLowerCase(), f);
  }

  const graph = analyzeFormRuleGraph(fields, rules);
  const effectiveValues: Record<string, unknown> = { ...rawValues };
  const visibleFields = new Set<string>();
  const requiredFields = new Set<string>();
  const readOnlyFields = new Set<string>();
  const computedFields = new Set<string>();

  if (graph.hasCycle) {
    // Fail safe in UI: mark all fields visible, surface cycle warning
    for (const f of dataFields) {
      visibleFields.add(f.name);
      if (f.required) requiredFields.add(f.name);
      if (f.readOnly) readOnlyFields.add(f.name);
    }
    return {
      effectiveValues,
      visibleFields,
      requiredFields,
      readOnlyFields,
      computedFields,
      hasCycle: true,
      cyclePath: graph.cyclePath
    };
  }

  const rulesByTarget = new Map<string, FormRule[]>();
  for (const r of rules ?? []) {
    if (!r.targetField) continue;
    const key = r.targetField.toLowerCase();
    const list = rulesByTarget.get(key) ?? [];
    list.push(r);
    rulesByTarget.set(key, list);
  }

  // Build evaluation order from topological sort
  const orderedFields: SmartFormField[] = [];
  const seen = new Set<string>();
  for (const nodeName of graph.topologicalOrder) {
    const f = fieldByLower.get(nodeName.toLowerCase());
    if (f && !seen.has(f.name.toLowerCase())) {
      seen.add(f.name.toLowerCase());
      orderedFields.push(f);
    }
  }
  for (const f of dataFields) {
    if (!seen.has(f.name.toLowerCase())) {
      seen.add(f.name.toLowerCase());
      orderedFields.push(f);
    }
  }

  for (const field of orderedFields) {
    const fieldRules = rulesByTarget.get(field.name.toLowerCase()) ?? [];

    // 1. Visibility
    let isVisible = evaluateLegacyVisibilityRule(field.visibilityRule, effectiveValues);
    if (isVisible) {
      for (const visRule of fieldRules.filter((r) => r.effect === 'visible')) {
        if (visRule.conditionJsonLogic && !evaluateJsonLogicBool(visRule.conditionJsonLogic, effectiveValues)) {
          isVisible = false;
          break;
        }
      }
    }

    if (!isVisible) {
      effectiveValues[field.name] = '';
      continue;
    }

    visibleFields.add(field.name);

    // 2. Computed value
    for (const compRule of fieldRules.filter((r) => r.effect === 'computedValue')) {
      const condOk =
        !compRule.conditionJsonLogic || evaluateJsonLogicBool(compRule.conditionJsonLogic, effectiveValues);
      if (condOk && compRule.expressionJsonLogic !== undefined && compRule.expressionJsonLogic !== null) {
        const computed = evaluateJsonLogic(compRule.expressionJsonLogic, effectiveValues);
        effectiveValues[field.name] = computed;
        computedFields.add(field.name);
      }
    }

    // 3. Required
    let isReq = field.required;
    if (!isReq) {
      for (const reqRule of fieldRules.filter((r) => r.effect === 'required')) {
        const condOk =
          !reqRule.conditionJsonLogic || evaluateJsonLogicBool(reqRule.conditionJsonLogic, effectiveValues);
        if (condOk) {
          isReq = true;
          break;
        }
      }
    }
    if (isReq) requiredFields.add(field.name);

    // 4. ReadOnly
    let isRo = field.readOnly || computedFields.has(field.name);
    if (!isRo) {
      for (const roRule of fieldRules.filter((r) => r.effect === 'readOnly')) {
        const condOk =
          !roRule.conditionJsonLogic || evaluateJsonLogicBool(roRule.conditionJsonLogic, effectiveValues);
        if (condOk) {
          isRo = true;
          break;
        }
      }
    }
    if (isRo) readOnlyFields.add(field.name);
  }

  return {
    effectiveValues,
    visibleFields,
    requiredFields,
    readOnlyFields,
    computedFields,
    hasCycle: false,
    cyclePath: []
  };
}
