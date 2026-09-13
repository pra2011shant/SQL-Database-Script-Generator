/**
 * SQL Database Script Generator - Enterprise Controller
 * - Monaco Editor + Monaco Diff Editor (Side-by-side comparison)
 * - Mermaid.js Interactive Entity-Relationship (ER) Diagram
 * - Live SQL Server Database Schema Inspector
 * - Corporate Policy / RAG Rules Manager
 * - Multi-Database Engine Transpiler
 */

let inputEditor = null;
let outputEditor = null;
let diffEditor = null;
let originalDiffModel = null;
let modifiedDiffModel = null;
let activeAbortController = null;
let currentMermaidCode = "erDiagram\n    CUSTOMERS ||--o{ ORDERS : places";
const templateCache = new Map();

// Debounce helper
function debounce(func, wait) {
    let timeout;
    return function (...args) {
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(this, args), wait);
    };
}

// Initialize Monaco Editors via RequireJS CDN
function initMonaco() {
    require.config({ paths: { vs: 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.45.0/min/vs' } });

    require(['vs/editor/editor.main'], function () {
        monaco.editor.defineTheme('sqlDarkModern', {
            base: 'vs-dark',
            inherit: true,
            rules: [
                { token: 'keyword', foreground: '38bdf8', fontStyle: 'bold' },
                { token: 'string', foreground: 'a5f3fc' },
                { token: 'number', foreground: 'fcd34d' },
                { token: 'comment', foreground: '64748b', fontStyle: 'italic' }
            ],
            colors: {
                'editor.background': '#111827',
                'editor.foreground': '#f8fafc',
                'editorLineNumber.foreground': '#475569',
                'editorLineNumber.activeForeground': '#38bdf8',
                'editorCursor.foreground': '#38bdf8',
                'editor.selectionBackground': '#1e293b',
                'editor.lineHighlightBackground': '#1a2234'
            }
        });

        const commonOptions = {
            language: 'sql',
            theme: 'sqlDarkModern',
            automaticLayout: true,
            fontSize: 13,
            fontFamily: "'JetBrains Mono', 'Fira Code', Consolas, monospace",
            minimap: { enabled: true, maxColumn: 40 },
            scrollBeyondLastLine: false,
            wordWrap: 'on',
            lineNumbers: 'on',
            renderWhitespace: 'selection',
            bracketPairColorization: { enabled: true },
            formatOnPaste: true
        };

        // Create Input Editor
        inputEditor = monaco.editor.create(document.getElementById('inputMonacoContainer'), {
            ...commonOptions,
            value: ''
        });

        // Create Output Editor
        outputEditor = monaco.editor.create(document.getElementById('outputMonacoContainer'), {
            ...commonOptions,
            value: '',
            readOnly: false
        });

        // Create Monaco Diff Editor
        diffEditor = monaco.editor.createDiffEditor(document.getElementById('diffMonacoContainer'), {
            theme: 'sqlDarkModern',
            automaticLayout: true,
            readOnly: true,
            renderSideBySide: true,
            fontSize: 12,
            fontFamily: "'JetBrains Mono', 'Fira Code', Consolas, monospace"
        });

        originalDiffModel = monaco.editor.createModel('-- Original Input Script', 'sql');
        modifiedDiffModel = monaco.editor.createModel('-- Generated / Optimized Script', 'sql');
        diffEditor.setModel({ original: originalDiffModel, modified: modifiedDiffModel });

        // Debounced metrics update
        const updateMetricsDebounced = debounce(() => {
            if (!inputEditor) return;
            const model = inputEditor.getModel();
            if (model) {
                const lines = model.getLineCount();
                const chars = model.getValueLength();
                const countElem = document.getElementById('inputLineCount');
                if (countElem) countElem.innerText = `${lines} lines (${chars} chars)`;
            }
        }, 150);

        inputEditor.onDidChangeModelContent(updateMetricsDebounced);

        // Position tracking
        inputEditor.onDidChangeCursorPosition(e => {
            const pos = e.position;
            const posElem = document.getElementById('inputPosStatus');
            if (posElem) posElem.innerText = `Ln ${pos.lineNumber}, Col ${pos.column}`;
        });

        outputEditor.onDidChangeCursorPosition(e => {
            const pos = e.position;
            const posElem = document.getElementById('outputPosStatus');
            if (posElem) posElem.innerText = `Ln ${pos.lineNumber}, Col ${pos.column}`;
        });

        // Shortcut: Ctrl + Enter
        inputEditor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter, function () {
            const btn = document.getElementById('btnExecuteAction');
            if (btn) btn.click();
        });
    });
}

// Render Mermaid ER Diagram
async function renderSchemaDiagram() {
    const container = document.getElementById('mermaidDiagramContainer');
    if (!container || !window.mermaid) return;

    try {
        container.innerHTML = `<div class="mermaid text-light">${currentMermaidCode}</div>`;
        await mermaid.run({ nodes: container.querySelectorAll('.mermaid') });
    } catch (err) {
        container.innerHTML = `<div class="text-danger p-3"><i class="bi bi-exclamation-octagon me-2"></i> Error rendering ER diagram: ${err.message}</div>`;
    }
}

// Lazy Load & Cache Sample Templates from Backend
async function loadSample(sampleKey) {
    if (!sampleKey) return;

    try {
        let sqlText = '';
        if (templateCache.has(sampleKey)) {
            sqlText = templateCache.get(sampleKey);
        } else {
            const response = await fetch(`/Home/GetSampleTemplate?key=${encodeURIComponent(sampleKey)}`);
            if (response.ok) {
                const data = await response.json();
                sqlText = data.sql || '';
                templateCache.set(sampleKey, sqlText);
            }
        }

        if (inputEditor && sqlText) {
            inputEditor.setValue(sqlText);
        }

        const actionSelect = document.getElementById('actionSelect');
        const reqInput = document.getElementById('customRequirement');

        if (sampleKey === 'buggy_sql') {
            if (actionSelect) actionSelect.value = 'debug_sql';
            if (reqInput) reqInput.value = 'Fix all syntax and grammatical errors, format properly with semicolons.';
        } else if (sampleKey === 'optimize_query') {
            if (actionSelect) actionSelect.value = 'optimize_query';
            if (reqInput) reqInput.value = 'Optimize joins, avoid non-SARGable WHERE predicates, and suggest indexes.';
        } else if (sampleKey === 'stored_proc_input') {
            if (actionSelect) actionSelect.value = 'create_sp';
            if (reqInput) reqInput.value = 'Generate CRUD stored procedures with TRY...CATCH error handling and transactions.';
        } else {
            if (actionSelect) actionSelect.value = 'create_table';
            if (reqInput) reqInput.value = 'Generate enterprise schema with named constraints and audit tracking.';
        }
    } catch (err) {
        console.error('Error loading template:', err);
    }
}

// Add prompt chip
function addPromptChip(text) {
    const reqBox = document.getElementById('customRequirement');
    if (reqBox) {
        const current = reqBox.value.trim();
        if (!current) {
            reqBox.value = text;
        } else if (!current.includes(text)) {
            reqBox.value = `${current}, ${text}`;
        }
        reqBox.focus();
    }
}

// Copy output
function copyOutputToClipboard() {
    if (!outputEditor) return;
    const text = outputEditor.getValue();
    navigator.clipboard.writeText(text).then(() => {
        const btn = document.getElementById('btnCopyOutput');
        if (btn) {
            const originalHtml = btn.innerHTML;
            btn.innerHTML = `<i class="bi bi-check2"></i> Copied!`;
            setTimeout(() => { btn.innerHTML = originalHtml; }, 2000);
        }
    });
}

// Download output
function downloadOutputSql() {
    if (!outputEditor) return;
    const text = outputEditor.getValue();
    const blob = new Blob([text], { type: 'text/sql;charset=utf-8;' });
    const link = document.createElement('a');
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
    link.href = URL.createObjectURL(blob);
    link.download = `Generated_SQL_${timestamp}.sql`;
    link.click();
}

// Clear editors
function clearInputEditor() {
    if (inputEditor) {
        inputEditor.setValue('');
        inputEditor.focus();
    }
}

function clearOutputEditor() {
    if (outputEditor) {
        outputEditor.setValue('');
    }
}

// Switch layout view modes
function switchLayout(mode) {
    const inputCard = document.getElementById('inputPanelCard');
    const outputCard = document.getElementById('outputPanelCard');
    const grid = document.getElementById('editorsGrid');

    document.querySelectorAll('.view-tab-btn').forEach(btn => btn.classList.remove('active'));
    const targetTab = document.getElementById(`tab-${mode}`);
    if (targetTab) targetTab.classList.add('active');

    if (mode === 'split') {
        grid.style.gridTemplateColumns = '1fr 1fr';
        inputCard.style.display = 'flex';
        outputCard.style.display = 'flex';
    } else if (mode === 'input') {
        grid.style.gridTemplateColumns = '1fr';
        inputCard.style.display = 'flex';
        outputCard.style.display = 'none';
    } else if (mode === 'output') {
        grid.style.gridTemplateColumns = '1fr';
        inputCard.style.display = 'none';
        outputCard.style.display = 'flex';
    }

    if (inputEditor) requestAnimationFrame(() => inputEditor.layout());
    if (outputEditor) requestAnimationFrame(() => outputEditor.layout());
    if (diffEditor) requestAnimationFrame(() => diffEditor.layout());
}

// Jump to line in input editor
function jumpToLine(line, col) {
    if (inputEditor && line > 0) {
        inputEditor.revealPositionInCenter({ lineNumber: line, column: col || 1 });
        inputEditor.setPosition({ lineNumber: line, column: col || 1 });
        inputEditor.focus();
    }
}

// Live DB Inspection API calls
async function inspectLiveDbTables() {
    const connStr = document.getElementById('liveDbConnString').value.trim();
    const statusMsg = document.getElementById('liveDbStatusMsg');
    const tableContainer = document.getElementById('liveDbTablesContainer');
    const select = document.getElementById('liveDbTableSelect');

    if (!connStr) {
        statusMsg.innerText = 'Please provide a valid SQL Server connection string.';
        return;
    }

    statusMsg.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Connecting and inspecting tables...';
    try {
        const response = await fetch('/Home/InspectLiveDatabaseTables', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ connectionString: connStr })
        });

        const data = await response.json();
        if (response.ok && data.success) {
            select.innerHTML = data.tables.map(t => `<option value="${t}">${t}</option>`).join('');
            tableContainer.style.display = 'block';
            statusMsg.innerHTML = `<span class="text-success"><i class="bi bi-check-circle"></i> Found ${data.tables.length} tables.</span>`;
        } else {
            statusMsg.innerText = data.message || 'Failed to inspect tables.';
        }
    } catch (err) {
        statusMsg.innerText = `Connection failed: ${err.message}`;
    }
}

async function loadSelectedLiveTableDdl() {
    const connStr = document.getElementById('liveDbConnString').value.trim();
    const tableName = document.getElementById('liveDbTableSelect').value;
    const statusMsg = document.getElementById('liveDbStatusMsg');

    if (!tableName) return;

    statusMsg.innerHTML = `<span class="spinner-border spinner-border-sm me-1"></span> Extracting DDL for ${tableName}...`;
    try {
        const response = await fetch('/Home/ExtractLiveTableDdl', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ connectionString: connStr, tableName: tableName })
        });

        const data = await response.json();
        if (response.ok && data.success) {
            if (inputEditor) inputEditor.setValue(data.ddl);
            statusMsg.innerHTML = `<span class="text-success"><i class="bi bi-check-circle"></i> Loaded into Editor!</span>`;
            bootstrap.Modal.getInstance(document.getElementById('liveDbModal')).hide();
        } else {
            statusMsg.innerText = data.message || 'Failed to extract DDL.';
        }
    } catch (err) {
        statusMsg.innerText = `Error: ${err.message}`;
    }
}

// Corporate Standards Load & Save
async function loadCorporateStandards() {
    try {
        const response = await fetch('/Home/GetCorporateStandards');
        if (response.ok) {
            const data = await response.json();
            document.getElementById('stdSpPrefix').value = data.spPrefix || 'usp_';
            document.getElementById('stdIndexPrefix').value = data.indexPrefix || 'IX_';
            document.getElementById('stdCreatedAt').value = data.createdAtColumn || 'CreatedAtUtc';
            document.getElementById('stdSoftDelete').value = data.softDeleteColumn || 'IsDeleted';
            document.getElementById('stdCreatedBy').value = data.createdByColumn || 'CreatedBy';
            document.getElementById('stdConcurrency').checked = data.enableOptimisticConcurrency ?? true;
        }
    } catch (err) {
        console.error('Failed to load standards:', err);
    }
}

async function saveCorporateStandards() {
    const payload = {
        spPrefix: document.getElementById('stdSpPrefix').value.trim(),
        indexPrefix: document.getElementById('stdIndexPrefix').value.trim(),
        createdAtColumn: document.getElementById('stdCreatedAt').value.trim(),
        softDeleteColumn: document.getElementById('stdSoftDelete').value.trim(),
        createdByColumn: document.getElementById('stdCreatedBy').value.trim(),
        enableOptimisticConcurrency: document.getElementById('stdConcurrency').checked
    };

    try {
        const response = await fetch('/Home/UpdateCorporateStandards', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        if (response.ok) {
            if (window.Swal) {
                Swal.fire({
                    icon: 'success',
                    title: 'Standards Saved',
                    text: 'Corporate SQL Standards & RAG Policy updated successfully!',
                    background: '#1e293b',
                    color: '#f8fafc',
                    confirmButtonColor: '#3b82f6',
                    timer: 2000,
                    showConfirmButton: false
                });
            }
            bootstrap.Modal.getInstance(document.getElementById('standardsModal')).hide();
        }
    } catch (err) {
        if (window.Swal) {
            Swal.fire({
                icon: 'error',
                title: 'Save Failed',
                text: err.message,
                background: '#1e293b',
                color: '#f8fafc',
                confirmButtonColor: '#ef4444'
            });
        }
    }
}

// Groq Cloud AI Configuration Load & Save
async function loadAiConfig() {
    try {
        const response = await fetch('/Home/GetAiConfig');
        if (response.ok) {
            const data = await response.json();

            const groqKey = document.getElementById('cfgGroqKey');
            if (groqKey) groqKey.value = data.groqApiKey || '';

            const groqModel = document.getElementById('cfgGroqModel');
            if (groqModel) groqModel.value = data.groqModel || 'llama-3.3-70b-versatile';

            const enableAi = document.getElementById('cfgEnableAi');
            if (enableAi) enableAi.checked = data.enableAiEnhancement ?? true;

            const fallbackAst = document.getElementById('cfgFallbackAst');
            if (fallbackAst) fallbackAst.checked = data.fallbackToLocalGenerator ?? true;

            updateAiBadge(data.groqApiKey);
        }
    } catch (err) {
        console.error('Failed to load AI config:', err);
    }
}

function updateAiBadge(hasKey) {
    const badge = document.getElementById('navAiProviderBadge');
    if (badge) {
        if (hasKey && hasKey.trim().length > 5) {
            badge.innerText = 'AI: Groq (llama-3.3-70b)';
        } else {
            badge.innerText = 'AI: Groq (Set Key)';
        }
    }
}

async function saveAiConfig() {
    const groqKey = document.getElementById('cfgGroqKey') ? document.getElementById('cfgGroqKey').value.trim() : '';
    const groqModel = document.getElementById('cfgGroqModel') ? document.getElementById('cfgGroqModel').value.trim() : 'llama-3.3-70b-versatile';
    const enableAi = document.getElementById('cfgEnableAi') ? document.getElementById('cfgEnableAi').checked : true;
    const fallbackAst = document.getElementById('cfgFallbackAst') ? document.getElementById('cfgFallbackAst').checked : true;

    const payload = {
        groqApiKey: groqKey,
        groqModel: groqModel || 'llama-3.3-70b-versatile',
        enableAiEnhancement: enableAi,
        fallbackToLocalGenerator: fallbackAst,
        timeoutSeconds: 30
    };

    try {
        const response = await fetch('/Home/UpdateAiConfig', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        if (response.ok) {
            updateAiBadge(payload.groqApiKey);
            if (window.Swal) {
                Swal.fire({
                    icon: 'success',
                    title: 'Groq Cloud Configured',
                    text: payload.groqApiKey ? 'Groq AI (llama-3.3-70b) is active and ready!' : 'Offline fallback active until Groq API key is set.',
                    background: '#1e293b',
                    color: '#f8fafc',
                    confirmButtonColor: '#22c55e',
                    timer: 2000,
                    showConfirmButton: false
                });
            }
            const modalEl = document.getElementById('aiConfigModal');
            if (modalEl) {
                const modalInstance = bootstrap.Modal.getInstance(modalEl);
                if (modalInstance) modalInstance.hide();
            }
        }
    } catch (err) {
        if (window.Swal) {
            Swal.fire({
                icon: 'error',
                title: 'Save Failed',
                text: err.message,
                background: '#1e293b',
                color: '#f8fafc',
                confirmButtonColor: '#ef4444'
            });
        }
    }
}

// DOM Event Bindings
document.addEventListener('DOMContentLoaded', () => {
    initMonaco();
    loadAiConfig();

    // Monaco layout & diff / diagram triggers on output tab change
    const codeTabBtn = document.getElementById('tab-code-btn');
    if (codeTabBtn) {
        codeTabBtn.addEventListener('shown.bs.tab', () => {
            if (outputEditor) requestAnimationFrame(() => outputEditor.layout());
        });
    }

    const diffTabBtn = document.getElementById('tab-diff-btn');
    if (diffTabBtn) {
        diffTabBtn.addEventListener('shown.bs.tab', () => {
            if (diffEditor) requestAnimationFrame(() => diffEditor.layout());
        });
    }

    const diagramTabBtn = document.getElementById('tab-diagram-btn');
    if (diagramTabBtn) {
        diagramTabBtn.addEventListener('shown.bs.tab', () => {
            renderSchemaDiagram();
        });
    }

    // Execute Action button handler
    const btnExec = document.getElementById('btnExecuteAction');
    if (btnExec) {
        btnExec.addEventListener('click', async () => {
            let inputSql = inputEditor ? inputEditor.getValue().trim() : '';
            const action = document.getElementById('actionSelect').value;
            const requirement = document.getElementById('customRequirement') ? document.getElementById('customRequirement').value.trim() : '';

            if (!inputSql && !requirement) {
                if (window.Swal) {
                    Swal.fire({
                        icon: 'info',
                        title: 'Input Required',
                        html: 'Please enter instructions in the <b>Requirements</b> box or paste SQL in the <b>Input Panel</b>.',
                        background: '#1e293b',
                        color: '#f8fafc',
                        confirmButtonColor: '#3b82f6',
                        confirmButtonText: '<i class="bi bi-check2"></i> Got it',
                        customClass: {
                            popup: 'border border-secondary'
                        }
                    });
                }
                return;
            }

            if (!inputSql && requirement) {
                inputSql = requirement;
            }

            if (activeAbortController) {
                activeAbortController.abort();
            }
            activeAbortController = new AbortController();

            btnExec.disabled = true;
            btnExec.innerHTML = `<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Processing...`;

            try {
                const response = await fetch('/Home/ProcessSql', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        sqlInput: inputSql,
                        action: action,
                        requirement: requirement,
                        databaseEngine: 'SqlServer2022',
                        includeTryCatch: true,
                        includeTransactions: true,
                        includeComments: true
                    }),
                    signal: activeAbortController.signal
                });

                if (response.ok) {
                    const data = await response.json();
                    const resultText = data.resultSql || data.formattedSql || '-- No output returned';

                    if (outputEditor) {
                        outputEditor.setValue(resultText);
                    }

                    // Update Diff Editor Models
                    if (originalDiffModel && modifiedDiffModel) {
                        originalDiffModel.setValue(inputSql);
                        modifiedDiffModel.setValue(resultText);
                    }

                    // Update ER Diagram
                    currentMermaidCode = data.mermaidErDiagram || "erDiagram\n    TARGET_TABLE {\n        int ID PK\n    }";
                    renderSchemaDiagram();

                    // Populate Analysis & Explanation Cards
                    const diagElem = document.getElementById('analysisDiagnosisText');
                    if (diagElem) {
                        diagElem.innerText = data.diagnosis || 'No issues detected during analysis.';
                    }

                    const expElem = document.getElementById('analysisExplanationText');
                    if (expElem) {
                        expElem.innerText = data.explanation || 'Processed successfully using modern T-SQL standards.';
                    }

                    const recList = document.getElementById('analysisRecommendationsList');
                    if (recList && data.recommendations && data.recommendations.length > 0) {
                        recList.innerHTML = data.recommendations.map(r => `<li>${r}</li>`).join('');
                    } else if (recList) {
                        recList.innerHTML = `<li>Follow standard database normalization and indexing guidelines.</li>`;
                    }

                    // Update status bar & diagnostics
                    const statusDot = document.getElementById('syntaxStatusDot');
                    const statusText = document.getElementById('syntaxStatusText');
                    const drawer = document.getElementById('diagnosticsDrawer');
                    const diagList = document.getElementById('diagnosticsList');

                    if (data.isValid) {
                        statusDot.className = 'status-dot valid';
                        statusText.innerText = `Syntax Valid (${data.executionTimeMs || 0}ms)`;
                        drawer.classList.remove('show');
                        document.getElementById('batchMetricBadge').innerText = `${data.batchCount || 1} Batches`;
                        document.getElementById('stmtMetricBadge').innerText = `${data.statementCount || 0} Statements`;
                    } else {
                        statusDot.className = 'status-dot invalid';
                        statusText.innerText = `${data.errors ? data.errors.length : 0} Syntax Errors`;
                        
                        if (data.errors && data.errors.length > 0) {
                            diagList.innerHTML = data.errors.map(err => `
                                <div class="diagnostic-item" onclick="jumpToLine(${err.line}, ${err.column})">
                                    <i class="bi bi-x-circle-fill text-danger"></i>
                                    <strong>Line ${err.line}, Col ${err.column}:</strong> ${err.message}
                                </div>
                            `).join('');
                            drawer.classList.add('show');
                        }
                    }
                } else {
                    if (outputEditor) {
                        outputEditor.setValue(`-- Error executing request: Server responded with HTTP status ${response.status}`);
                    }
                }
            } catch (err) {
                if (err.name !== 'AbortError') {
                    console.error(err);
                    if (outputEditor) {
                        outputEditor.setValue(`-- Request failed: ${err.message}`);
                    }
                }
            } finally {
                btnExec.disabled = false;
                btnExec.innerHTML = `<i class="bi bi-lightning-charge-fill"></i> Generate Script`;
                activeAbortController = null;
            }
        });
    }
});
