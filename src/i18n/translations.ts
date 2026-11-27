export type SupportedLanguage = 'en' | 'fr' | 'ar';
export type Direction = 'ltr' | 'rtl';

export interface Translations {
  common: {
    appName: string;
    studioName: string;
    version: string;
    tagline: string;
    searchPlaceholder: string;
    notifications: string;
    pending: string;
    environment: string;
    themeToggle: string;
    switchToDark: string;
    switchToLight: string;
    directionToggle: string;
    language: string;
  };
  toolbar: {
    testSimulation: string;
    simulating: string;
    nextStep: string;
    stopSimulation: string;
    validate: string;
    saveDefinition: string;
    savedToDb: string;
    saving: string;
    unsavedChanges: string;
    allChangesSaved: string;
    moreActions: string;
    exportJson: string;
    exportJsonDesc: string;
    importJson: string;
    importJsonDesc: string;
    savedDb: string;
    savedDbDesc: string;
    resetCanvas: string;
    resetCanvasDesc: string;
    demoMode: string;
    connectedMode: string;
    runtimeSelector: string;
  };
  toolbox: {
    title: string;
    nodesCount: string;
    sampleTemplates: string;
    dragDropComponents: string;
    dragHint: string;
    quickAdd: string;
    templates: {
      onboarding: string;
      expense: string;
      itAccess: string;
      blank: string;
    };
    nodes: {
      trigger: { label: string; category: string; description: string };
      approval: { label: string; category: string; description: string };
      sendEmail: { label: string; category: string; description: string };
      decision: { label: string; category: string; description: string };
      end: { label: string; category: string; description: string };
      stage: { label: string; category: string; description: string };
    };
  };
  canvas: {
    zoomIn: string;
    zoomOut: string;
    fitView: string;
    minimap: string;
  };
  drawer: {
    noNodeSelected: string;
    noNodeHint: string;
    inspectorTitle: string;
    deleteNode: string;
    closeDrawer: string;
    draftAutoSaved: string;
  };
  nav: {
    dashboard: string;
    portal: string;
    designer: string;
    smartforms: string;
    instances: string;
    smartobjects: string;
    tasks: string;
    analytics: string;
    users: string;
    settings: string;
    collapseSidebar: string;
    expandSidebar: string;
    engineFooter: string;
  };
  profile: {
    selectLanguage: string;
    selectEnvironment: string;
    requestedBy: string;
    due: string;
    noPendingNotifications: string;
    switchRuntimeMode: string;
    changePassword: string;
    signOut: string;
    currentPassword: string;
    newPassword: string;
    savePassword: string;
    cancel: string;
    passwordUpdated: string;
    connectedSession: string;
    demoSession: string;
  };
}

export const translations: Record<SupportedLanguage, Translations> = {
  en: {
    common: {
      appName: 'TAMAM',
      studioName: 'WORKFLOW STUDIO',
      version: 'v4.8 Enterprise',
      tagline: 'Enterprise Process Automation Platform',
      searchPlaceholder: 'Search workflows, tasks, SmartObjects, instances...',
      notifications: 'Notifications',
      pending: 'Pending',
      environment: 'Environment',
      themeToggle: 'Toggle light and dark theme',
      switchToDark: 'Switch to Dark Mode',
      switchToLight: 'Switch to Light Mode',
      directionToggle: 'Switch layout direction (LTR / RTL)',
      language: 'Language',
    },
    toolbar: {
      testSimulation: 'Test Simulation',
      simulating: 'Simulating Execution...',
      nextStep: 'Next Step',
      stopSimulation: 'Stop',
      validate: 'Validate',
      saveDefinition: 'Save Definition',
      savedToDb: 'Saved to DB!',
      saving: 'Saving...',
      unsavedChanges: 'Unsaved changes',
      allChangesSaved: 'All changes saved',
      moreActions: 'More actions',
      exportJson: 'Export JSON',
      exportJsonDesc: 'Export workflow definition to JSON file',
      importJson: 'Import JSON',
      importJsonDesc: 'Import workflow definition from JSON file',
      savedDb: 'Saved DB',
      savedDbDesc: 'Open saved workflow versions database',
      resetCanvas: 'Reset Canvas',
      resetCanvasDesc: 'Clear workflow canvas',
      demoMode: 'Demo Mode',
      connectedMode: 'Connected: Live API',
      runtimeSelector: 'Click to switch between Demo and Connected mode',
    },
    toolbox: {
      title: 'Workflow Toolbox',
      nodesCount: 'Nodes',
      sampleTemplates: 'Sample Templates',
      dragDropComponents: 'Drag & Drop Components',
      dragHint: '💡 Drag cards onto canvas or click +',
      quickAdd: 'Quick add to canvas',
      templates: {
        onboarding: '📋 Onboarding',
        expense: '💳 Expense Appr.',
        itAccess: '🔑 IT System',
        blank: '✨ Blank Canvas',
      },
      nodes: {
        trigger: {
          label: 'Start / Trigger',
          category: 'Triggers',
          description: 'Defines event starting the workflow',
        },
        approval: {
          label: 'Approval Task',
          category: 'Tasks',
          description: 'User or Group decision task with dynamic outcome handles',
        },
        sendEmail: {
          label: 'Send Email',
          category: 'Notifications',
          description: 'Send automated email notification',
        },
        decision: {
          label: 'Decision / Rule',
          category: 'Logic',
          description: 'Conditional logical branching (IF / THEN)',
        },
        end: {
          label: 'End Event',
          category: 'End',
          description: 'Terminal outcome node for process execution',
        },
        stage: {
          label: 'Workflow Stage',
          category: 'Stages',
          description: 'Composite sub-process with isolated workflow logic & canvas',
        },
      },
    },
    canvas: {
      zoomIn: 'Zoom In',
      zoomOut: 'Zoom Out',
      fitView: 'Fit View to Screen',
      minimap: 'Workflow Overview Minimap',
    },
    drawer: {
      noNodeSelected: 'No Node Selected',
      noNodeHint: 'Click any node on the canvas to configure its properties, assignees, or dynamic decision outcome handles.',
      inspectorTitle: 'Node Configuration',
      deleteNode: 'Delete this node',
      closeDrawer: 'Close inspector drawer',
      draftAutoSaved: 'Draft auto-saved',
    },
    nav: {
      dashboard: 'Home Dashboard',
      portal: 'Service Portal',
      designer: 'Workflow Designer',
      smartforms: 'Form Designer (SmartForms)',
      instances: 'Active Instances',
      smartobjects: 'Data Models (SmartObjects)',
      tasks: 'Task Center',
      analytics: 'Reports & Analytics',
      users: 'Users & Access',
      settings: 'System Settings',
      collapseSidebar: 'Collapse Sidebar',
      expandSidebar: 'Expand Sidebar',
      engineFooter: 'Tamam Workflow Engine',
    },
    profile: {
      selectLanguage: 'Select Language',
      selectEnvironment: 'Select Environment',
      requestedBy: 'Requested by',
      due: 'Due',
      noPendingNotifications: 'No pending notifications',
      switchRuntimeMode: 'Switch Runtime Mode / Login',
      changePassword: 'Change Password',
      signOut: 'Sign Out',
      currentPassword: 'Current Password',
      newPassword: 'New Password (min 8 chars)',
      savePassword: 'Update Password',
      cancel: 'Cancel',
      passwordUpdated: 'Password updated!',
      connectedSession: 'Connected API Session',
      demoSession: 'Local Demo Session',
    },
  },
  fr: {
    common: {
      appName: 'TAMAM',
      studioName: 'WORKFLOW STUDIO',
      version: 'v4.8 Entreprise',
      tagline: 'Plateforme d’automatisation des processus d’entreprise',
      searchPlaceholder: 'Rechercher des flux, tâches, SmartObjects, instances...',
      notifications: 'Notifications',
      pending: 'En attente',
      environment: 'Environnement',
      themeToggle: 'Changer le thème clair/sombre',
      switchToDark: 'Passer en mode sombre',
      switchToLight: 'Passer en mode clair',
      directionToggle: 'Changer la direction (LTR / RTL)',
      language: 'Langue',
    },
    toolbar: {
      testSimulation: 'Tester la simulation',
      simulating: 'Simulation en cours...',
      nextStep: 'Étape suivante',
      stopSimulation: 'Arrêter',
      validate: 'Valider',
      saveDefinition: 'Enregistrer la définition',
      savedToDb: 'Enregistré en BD !',
      saving: 'Enregistrement...',
      unsavedChanges: 'Modifications non enregistrées',
      allChangesSaved: 'Toutes les modifications sont enregistrées',
      moreActions: 'Plus d’actions',
      exportJson: 'Exporter JSON',
      exportJsonDesc: 'Exporter la définition du flux en fichier JSON',
      importJson: 'Importer JSON',
      importJsonDesc: 'Importer la définition du flux depuis un fichier JSON',
      savedDb: 'BD Enregistrée',
      savedDbDesc: 'Ouvrir les versions sauvegardées dans la base',
      resetCanvas: 'Réinitialiser',
      resetCanvasDesc: 'Effacer le canevas de flux',
      demoMode: 'Mode Démo',
      connectedMode: 'Connecté : API en direct',
      runtimeSelector: 'Cliquer pour changer entre Démo et Connecté',
    },
    toolbox: {
      title: 'Boîte à outils de flux',
      nodesCount: 'Nœuds',
      sampleTemplates: 'Modèles exemples',
      dragDropComponents: 'Composants Glisser-Déposer',
      dragHint: '💡 Glissez les cartes sur le canevas ou cliquez sur +',
      quickAdd: 'Ajout rapide au canevas',
      templates: {
        onboarding: '📋 Intégration',
        expense: '💳 Dépense',
        itAccess: '🔑 Accès IT',
        blank: '✨ Canevas vierge',
      },
      nodes: {
        trigger: {
          label: 'Début / Déclencheur',
          category: 'Déclencheurs',
          description: 'Définit l’événement qui démarre le flux',
        },
        approval: {
          label: 'Tâche d’approbation',
          category: 'Tâches',
          description: 'Décision utilisateur ou groupe avec branches dynamiques',
        },
        sendEmail: {
          label: 'Envoyer un e-mail',
          category: 'Notifications',
          description: 'Envoyer une notification automatique par e-mail',
        },
        decision: {
          label: 'Décision / Règle',
          category: 'Logique',
          description: 'Branchement logique conditionnel (SI / ALORS)',
        },
        end: {
          label: 'Événement de fin',
          category: 'Fin',
          description: 'Nœud de résultat terminal pour l’exécution',
        },
        stage: {
          label: 'Étape / Sous-processus',
          category: 'Étapes',
          description: 'Sous-processus composite avec canevas et logique isolés',
        },
      },
    },
    canvas: {
      zoomIn: 'Zoom avant',
      zoomOut: 'Zoom arrière',
      fitView: 'Ajuster à l’écran',
      minimap: 'Mini-carte d’aperçu',
    },
    drawer: {
      noNodeSelected: 'Aucun nœud sélectionné',
      noNodeHint: 'Cliquez sur un nœud pour configurer ses propriétés.',
      inspectorTitle: 'Configuration du nœud',
      deleteNode: 'Supprimer ce nœud',
      closeDrawer: 'Fermer l’inspecteur',
      draftAutoSaved: 'Brouillon auto-enregistré',
    },
    nav: {
      dashboard: 'Tableau de bord',
      portal: 'Portail de Services',
      designer: 'Studio de Workflow',
      smartforms: 'Formulaires (SmartForms)',
      instances: 'Instances actives',
      smartobjects: 'Modèles de données (SmartObjects)',
      tasks: 'Centre de tâches',
      analytics: 'Rapports & Analytique',
      users: 'Utilisateurs & Accès',
      settings: 'Paramètres système',
      collapseSidebar: 'Réduire le menu',
      expandSidebar: 'Développer le menu',
      engineFooter: 'Moteur Tamam Workflow',
    },
    profile: {
      selectLanguage: 'Choisir la langue',
      selectEnvironment: 'Choisir l’environnement',
      requestedBy: 'Demandé par',
      due: 'Échéance',
      noPendingNotifications: 'Aucune notification en attente',
      switchRuntimeMode: 'Changer de mode / Connexion',
      changePassword: 'Changer le mot de passe',
      signOut: 'Se déconnecter',
      currentPassword: 'Mot de passe actuel',
      newPassword: 'Nouveau mot de passe (min 8 car.)',
      savePassword: 'Mettre à jour',
      cancel: 'Annuler',
      passwordUpdated: 'Mot de passe mis à jour !',
      connectedSession: 'Session API Connectée',
      demoSession: 'Session Démo Locale',
    },
  },
  ar: {
    common: {
      appName: 'تمام',
      studioName: 'استوديو تدفق العمل',
      version: 'الإصدار 4.8 للمؤسسات',
      tagline: 'منصة أتمتة العمليات للمؤسسات',
      searchPlaceholder: 'البحث في مسارات العمل، المهام، الكائنات الذكية...',
      notifications: 'الإشعارات',
      pending: 'قيد الانتظار',
      environment: 'البيئة',
      themeToggle: 'تبديل المظهر الفاتح والداكن',
      switchToDark: 'التبديل إلى الوضع الداكن',
      switchToLight: 'التبديل إلى الوضع الفاتح',
      directionToggle: 'تبديل اتجاه الواجهة (يمين/يسار)',
      language: 'اللغة',
    },
    toolbar: {
      testSimulation: 'محاكاة التنفيذ',
      simulating: 'جاري محاكاة التنفيذ...',
      nextStep: 'الخطوة التالية',
      stopSimulation: 'إيقاف',
      validate: 'تحقق من المخطط',
      saveDefinition: 'حفظ المخطط',
      savedToDb: 'تم الحفظ في القاعدة!',
      saving: 'جاري الحفظ...',
      unsavedChanges: 'تغييرات غير محفوظة',
      allChangesSaved: 'تم حفظ كافة التغييرات',
      moreActions: 'المزيد من الإجراءات',
      exportJson: 'تصدير JSON',
      exportJsonDesc: 'تصدير مسار العمل إلى ملف JSON',
      importJson: 'استيراد JSON',
      importJsonDesc: 'استيراد مسار العمل من ملف JSON',
      savedDb: 'القاعدة المحفوظة',
      savedDbDesc: 'فتح قاعدة الإصدارات المحفوظة',
      resetCanvas: 'إعادة ضبط اللوحة',
      resetCanvasDesc: 'مسح لوحة تدفق العمل',
      demoMode: 'الوضع التجريبي',
      connectedMode: 'متصل: خادم .NET 10',
      runtimeSelector: 'انقر للتبديل بين التجريبي والمتصل',
    },
    toolbox: {
      title: 'صندوق أدوات مسار العمل',
      nodesCount: 'عقد',
      sampleTemplates: 'نماذج جاهزة',
      dragDropComponents: 'عناصر السحب والإفلات',
      dragHint: '💡 اسحب البطاقات إلى اللوحة أو انقر على +',
      quickAdd: 'إضافة سريعة إلى اللوحة',
      templates: {
        onboarding: '📋 تدريب الموظف',
        expense: '💳 طلب شراء',
        itAccess: '🔑 صلاحيات النظام',
        blank: '✨ لوحة فارغة',
      },
      nodes: {
        trigger: {
          label: 'البدء / المشغل',
          category: 'المشغلات',
          description: 'يحدد الحدث الذي يبدأ مسار العمل',
        },
        approval: {
          label: 'مهمة موافقة',
          category: 'المهام',
          description: 'مهمة قرار للمستخدم أو المجموعة مع خيارات ديناميكية',
        },
        sendEmail: {
          label: 'إرسال بريد',
          category: 'الإشعارات',
          description: 'إرسال إشعار بريد إلكتروني تلقائي',
        },
        decision: {
          label: 'قرار / شرط',
          category: 'المنطق',
          description: 'تفرع شرطي منطقي (إذا / ثم)',
        },
        end: {
          label: 'حدث النهاية',
          category: 'النهاية',
          description: 'عقدة النتيجة النهائية لمسار العمل',
        },
        stage: {
          label: 'مرحلة مسار العمل',
          category: 'المراحل',
          description: 'عملية فرعية مجمعة لها منطق ولوحة عمل خاصة بها للتصميم السلس',
        },
      },
    },
    canvas: {
      zoomIn: 'تكبير',
      zoomOut: 'تصغير',
      fitView: 'ملاءمة الشاشة',
      minimap: 'الخريطة المصغرة',
    },
    drawer: {
      noNodeSelected: 'لم يتم تحديد أي عقدة',
      noNodeHint: 'انقر على أي عقدة في اللوحة لضبط خصائصها.',
      inspectorTitle: 'إعدادات العقدة',
      deleteNode: 'حذف هذه العقدة',
      closeDrawer: 'إغلاق لوحة الفحص',
      draftAutoSaved: 'تم الحفظ التلقائي للمسودة',
    },
    nav: {
      dashboard: 'لوحة القيادة الرئيسية',
      portal: 'بوابة الخدمات',
      designer: 'مصمم مسارات العمل',
      smartforms: 'مصمم النماذج الذكية (SmartForms)',
      instances: 'العمليات النشطة',
      smartobjects: 'نماذج البيانات (SmartObjects)',
      tasks: 'مركز المهام والموافقات',
      analytics: 'التقارير والتحليلات',
      users: 'المستخدمون والصلاحيات',
      settings: 'إعدادات النظام',
      collapseSidebar: 'طي القائمة الجانبية',
      expandSidebar: 'توسيع القائمة الجانبية',
      engineFooter: 'محرك تمام لتدفق العمل',
    },
    profile: {
      selectLanguage: 'اختر اللغة',
      selectEnvironment: 'اختر البيئة',
      requestedBy: 'بطلب من',
      due: 'الاستحقاق',
      noPendingNotifications: 'لا توجد إشعارات معلقة',
      switchRuntimeMode: 'تبديل وضع التشغيل / تسجيل الدخول',
      changePassword: 'تغيير كلمة المرور',
      signOut: 'تسجيل الخروج',
      currentPassword: 'كلمة المرور الحالية',
      newPassword: 'كلمة المرور الجديدة (8 أحرف على الأقل)',
      savePassword: 'تحديث كلمة المرور',
      cancel: 'إلغاء',
      passwordUpdated: 'تم تحديث كلمة المرور بنجاح!',
      connectedSession: 'جلسة خادم متصلة',
      demoSession: 'جلسة تجريبية محلية',
    },
  },
};
