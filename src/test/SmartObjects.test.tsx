import { describe, it, expect, beforeEach } from 'vitest';
import '@testing-library/jest-dom/vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { useWorkflowStore } from '../store/useWorkflowStore';
import { DataModelsView } from '../components/views/DataModelsView';

describe('SmartObjects Entity & Data Items CRUD', () => {
  beforeEach(() => {
    // Reset or prepare store state if needed
  });

  it('can create, update, and delete a SmartObject Entity via store', async () => {
    const store = useWorkflowStore.getState();

    // 1. Create
    const created = await store.createSmartObject({
      name: 'ProductCatalog',
      displayName: 'Product Catalog',
      description: 'Master catalog of enterprise hardware & software licenses.',
      category: 'Core',
      fields: [
        { name: 'SKU', type: 'String', isPrimaryKey: true, isRequired: true },
        { name: 'Title', type: 'String', isRequired: true },
        { name: 'UnitPrice', type: 'Number', isRequired: true }
      ]
    });

    expect(created.id).toBeDefined();
    expect(created.name).toBe('ProductCatalog');
    expect(useWorkflowStore.getState().smartObjects.some(s => s.id === created.id)).toBe(true);

    // 2. Update
    await store.updateSmartObject(created.id, {
      displayName: 'Enterprise Product Catalog Updated',
      category: 'Finance'
    });

    const updated = useWorkflowStore.getState().smartObjects.find(s => s.id === created.id);
    expect(updated?.displayName).toBe('Enterprise Product Catalog Updated');
    expect(updated?.category).toBe('Finance');

    // 3. Delete
    await store.deleteSmartObject(created.id);
    expect(useWorkflowStore.getState().smartObjects.some(s => s.id === created.id)).toBe(false);
  });

  it('can manage fields on a SmartObject Entity via store', () => {
    const store = useWorkflowStore.getState();
    const targetSO = store.smartObjects[0];
    expect(targetSO).toBeDefined();

    const initialFieldCount = targetSO.fields.length;
    const testFieldName = `Field_${Date.now()}`;

    // Add field
    store.addSmartObjectField(targetSO.id, {
      name: testFieldName,
      type: 'String',
      isRequired: false
    });

    const afterAdd = useWorkflowStore.getState().smartObjects.find(s => s.id === targetSO.id);
    expect(afterAdd?.fields.length).toBe(initialFieldCount + 1);
    expect(afterAdd?.fields.some(f => f.name === testFieldName)).toBe(true);

    // Delete field
    store.deleteSmartObjectField(targetSO.id, testFieldName);
    const afterDelete = useWorkflowStore.getState().smartObjects.find(s => s.id === targetSO.id);
    expect(afterDelete?.fields.length).toBe(initialFieldCount);
    expect(afterDelete?.fields.some(f => f.name === testFieldName)).toBe(false);
  });

  it('can fetch, add, update, and delete data items (records) for SmartObjects', async () => {
    const store = useWorkflowStore.getState();
    const testSO = await store.createSmartObject({
      name: 'TestItemEntity',
      displayName: 'Test Item Entity',
      description: 'Test entity for record CRUD',
      category: 'IT',
      fields: [
        { name: 'ItemCode', type: 'String', isPrimaryKey: true, isRequired: true },
        { name: 'ItemName', type: 'String', isRequired: true },
        { name: 'Quantity', type: 'Number' }
      ]
    });

    // 1. Fetch initially (should be empty array for new SO)
    const initialRecords = await store.fetchSmartObjectRecords(testSO.id);
    expect(initialRecords).toEqual([]);

    // 2. Add Record
    await store.addSmartObjectRecord(testSO.id, {
      ItemCode: 'CODE-999',
      ItemName: 'Server Rack Mount',
      Quantity: 12
    });

    const recordsAfterAdd = useWorkflowStore.getState().smartObjectData[testSO.id];
    expect(recordsAfterAdd.length).toBe(1);
    expect(recordsAfterAdd[0].ItemCode).toBe('CODE-999');
    expect(recordsAfterAdd[0].Quantity).toBe(12);

    // 3. Update Record
    await store.updateSmartObjectRecord(testSO.id, 'CODE-999', {
      ItemName: 'Server Rack Mount Pro v2',
      Quantity: 25
    });

    const recordsAfterUpdate = useWorkflowStore.getState().smartObjectData[testSO.id];
    expect(recordsAfterUpdate[0].ItemName).toBe('Server Rack Mount Pro v2');
    expect(recordsAfterUpdate[0].Quantity).toBe(25);

    // 4. Delete Record
    await store.deleteSmartObjectRecord(testSO.id, 'CODE-999');
    const recordsAfterDelete = useWorkflowStore.getState().smartObjectData[testSO.id];
    expect(recordsAfterDelete.length).toBe(0);

    // Cleanup
    await store.deleteSmartObject(testSO.id);
  });

  it('renders DataModelsView with schema and records tabs, allowing navigation', async () => {
    render(<DataModelsView />);

    // Check heading
    expect(screen.getByText(/SmartObjects Data Models & Schema Builder/i)).toBeInTheDocument();

    // Check tabs
    const schemaTab = screen.getByTestId('tab-schema');
    const recordsTab = screen.getByTestId('tab-records');
    expect(schemaTab).toBeInTheDocument();
    expect(recordsTab).toBeInTheDocument();

    // Switch to records tab
    fireEvent.click(recordsTab);
    expect(screen.getByTestId('search-records-input')).toBeInTheDocument();
    expect(screen.getByTestId('add-record-btn')).toBeInTheDocument();

    // Switch back to schema tab
    fireEvent.click(schemaTab);
    expect(screen.getByTestId('add-field-btn')).toBeInTheDocument();
  });

  it('opens New SmartObject Entity modal and creates an entity', async () => {
    render(<DataModelsView />);

    const newEntityBtn = screen.getByTestId('create-smartobject-btn');
    fireEvent.click(newEntityBtn);

    const nameInput = screen.getByTestId('new-entity-name-input');
    const submitBtn = screen.getByTestId('submit-create-entity-btn');

    fireEvent.change(nameInput, { target: { value: 'AssetInventory' } });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(screen.getAllByText(/AssetInventory/i).length).toBeGreaterThan(0);
    });
  });
});
