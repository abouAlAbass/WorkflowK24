import { describe, it, expect } from 'vitest';
import {
  analyzeFormRuleGraph,
  evaluateJsonLogic,
  evaluateSmartFormState
} from '../utils/jsonLogicEngine';
import type { FormRule, SmartFormField } from '../types/smartform';

describe('Isomorphic JSONLogic Rules Engine & DAG', () => {
  it('evaluates repeater sum() with row expressions identically to backend', () => {
    const data = {
      LineItems: [
        { Item: 'Laptop', Qty: 2, UnitPrice: 1200 },
        { Item: 'Mouse', Qty: 3, UnitPrice: 50 }
      ]
    };

    const total = evaluateJsonLogic(
      {
        sum: [
          { var: 'LineItems' },
          { '*': [{ var: 'Qty' }, { var: 'UnitPrice' }] }
        ]
      },
      data
    );

    expect(total).toBe(2550);
  });

  it('detects circular dependencies in the rule DAG', () => {
    const fields: SmartFormField[] = [
      { id: '1', name: 'FieldA', label: 'A', type: 'number', required: false, readOnly: false, width: 'half' },
      { id: '2', name: 'FieldB', label: 'B', type: 'number', required: false, readOnly: false, width: 'half' }
    ];

    const rules: FormRule[] = [
      {
        id: 'r1',
        targetField: 'FieldA',
        effect: 'computedValue',
        expressionJsonLogic: { '+': [{ var: 'FieldB' }, 1] }
      },
      {
        id: 'r2',
        targetField: 'FieldB',
        effect: 'computedValue',
        expressionJsonLogic: { '*': [{ var: 'FieldA' }, 2] }
      }
    ];

    const analysis = analyzeFormRuleGraph(fields, rules);
    expect(analysis.hasCycle).toBe(true);
    expect(analysis.cyclePath.length).toBeGreaterThanOrEqual(3);
  });

  it('evaluates visible, required, readOnly and computedValue in topological order', () => {
    const fields: SmartFormField[] = [
      {
        id: 'f-lines',
        name: 'LineItems',
        label: 'Line Items',
        type: 'repeater',
        required: false,
        readOnly: false,
        width: 'full',
        repeaterColumns: [
          { name: 'Item', label: 'Item', type: 'text' },
          { name: 'Amount', label: 'Amount', type: 'number' }
        ]
      },
      { id: 'f-total', name: 'Total', label: 'Total', type: 'currency', required: false, readOnly: false, width: 'half' },
      { id: 'f-just', name: 'Justification', label: 'Justification', type: 'textarea', required: false, readOnly: false, width: 'full' },
      { id: 'f-disc', name: 'Discount', label: 'Discount', type: 'number', required: false, readOnly: false, width: 'half' }
    ];

    const rules: FormRule[] = [
      {
        id: 'r-sum',
        targetField: 'Total',
        effect: 'computedValue',
        expressionJsonLogic: { sum: [{ var: 'LineItems' }, 'Amount'] }
      },
      {
        id: 'r-vis',
        targetField: 'Justification',
        effect: 'visible',
        conditionJsonLogic: { '>': [{ var: 'Total' }, 500] }
      },
      {
        id: 'r-req',
        targetField: 'Justification',
        effect: 'required',
        conditionJsonLogic: { '>': [{ var: 'Total' }, 500] }
      },
      {
        id: 'r-ro',
        targetField: 'Discount',
        effect: 'readOnly',
        conditionJsonLogic: { '<=': [{ var: 'Total' }, 1000] }
      }
    ];

    const state = evaluateSmartFormState(fields, rules, {
      LineItems: [{ Item: 'A', Amount: 400 }, { Item: 'B', Amount: 350 }],
      Total: 0,
      Justification: '',
      Discount: 0
    });

    expect(state.hasCycle).toBe(false);
    expect(state.effectiveValues.Total).toBe(750);
    expect(state.computedFields.has('Total')).toBe(true);
    expect(state.visibleFields.has('Justification')).toBe(true);
    expect(state.requiredFields.has('Justification')).toBe(true);
    expect(state.readOnlyFields.has('Discount')).toBe(true);
  });
});
