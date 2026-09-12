/**
 * SQL Database Script Generator - High-Performance Monaco Controller
 * - Zero hardcoded SQL queries (100% backend driven)
 * - Debounced input tracking (Zero UI lag / No screen freezes)
 * - Lazy loaded sample templates with local caching
 * - Non-blocking asynchronous AJAX fetch with AbortController
 */

let inputEditor = null;
let outputEditor = null;
let activeAbortController = null;
const templateCache = new Map();

// Debounce helper to keep UI 60fps smooth
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
        // Theme definition
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

        // Common Editor Options
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

        // Debounced metrics update (Prevents typing lag)
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

        // Cursor position tracking
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

        // Shortcut: Ctrl + Enter to Generate
        inputEditor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter, function () {
            const btn = document.getElementById('btnExecuteAction');
            if (btn) btn.click();
        });

        // Lazy load default customer orders schema
        loadSample('schema_customers');
    });
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

        // Set contextual requirement text
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
            if (actionSelect) actionSelect.value = 'create_sp';
            if (reqInput) reqInput.value = 'Generate full CRUD stored procedures with TRY...CATCH and transaction handling.';
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
}

// Jump to line in input editor
function jumpToLine(line, col) {
    if (inputEditor && line > 0) {
        inputEditor.revealPositionInCenter({ lineNumber: line, column: col || 1 });
        inputEditor.setPosition({ lineNumber: line, column: col || 1 });
        inputEditor.focus();
    }
}

// DOM Event Bindings
document.addEventListener('DOMContentLoaded', () => {
    initMonaco();

    // Monaco layout trigger on output tab change
    const codeTabBtn = document.getElementById('tab-code-btn');
    if (codeTabBtn) {
        codeTabBtn.addEventListener('shown.bs.tab', () => {
            if (outputEditor) {
                requestAnimationFrame(() => outputEditor.layout());
            }
        });
    }

    // Execute Action button handler
    const btnExec = document.getElementById('btnExecuteAction');
    if (btnExec) {
        btnExec.addEventListener('click', async () => {
            const inputSql = inputEditor ? inputEditor.getValue() : '';
            const action = document.getElementById('actionSelect').value;
            const requirement = document.getElementById('customRequirement').value;

            if (!inputSql.trim()) {
                alert('Please provide a SQL query or table schema in the Input Panel.');
                return;
            }

            // Abort previous in-flight request if any
            if (activeAbortController) {
                activeAbortController.abort();
            }
            activeAbortController = new AbortController();

            // Set loading state
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
                    if (outputEditor) {
                        outputEditor.setValue(data.resultSql || data.formattedSql || '-- No output returned');
                    }

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
