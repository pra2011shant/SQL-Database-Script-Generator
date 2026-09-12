/**
 * SQL Database Script Generator - Monaco Editor & UI Controller
 * Pure client-side UI controller: All SQL parsing, templates, and generation are handled on backend.
 */

let inputEditor = null;
let outputEditor = null;

// Initialize Monaco Editors via RequireJS CDN
function initMonaco() {
    require.config({ paths: { vs: 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.45.0/min/vs' } });

    require(['vs/editor/editor.main'], function () {
        // Register custom theme
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

        // Create Input Editor
        inputEditor = monaco.editor.create(document.getElementById('inputMonacoContainer'), {
            value: '',
            language: 'sql',
            theme: 'sqlDarkModern',
            automaticLayout: true,
            fontSize: 13,
            fontFamily: "'JetBrains Mono', 'Fira Code', 'Cascadia Code', Consolas, monospace",
            minimap: { enabled: true, maxColumn: 40 },
            scrollBeyondLastLine: false,
            wordWrap: 'on',
            lineNumbers: 'on',
            renderWhitespace: 'selection',
            bracketPairColorization: { enabled: true },
            formatOnPaste: true
        });

        // Create Output Editor
        outputEditor = monaco.editor.create(document.getElementById('outputMonacoContainer'), {
            value: '',
            language: 'sql',
            theme: 'sqlDarkModern',
            automaticLayout: true,
            fontSize: 13,
            fontFamily: "'JetBrains Mono', 'Fira Code', 'Cascadia Code', Consolas, monospace",
            minimap: { enabled: true, maxColumn: 40 },
            scrollBeyondLastLine: false,
            wordWrap: 'on',
            lineNumbers: 'on',
            readOnly: false,
            bracketPairColorization: { enabled: true }
        });

        // Track cursor position in input editor
        inputEditor.onDidChangeCursorPosition(e => {
            const pos = e.position;
            document.getElementById('inputPosStatus').innerText = `Ln ${pos.lineNumber}, Col ${pos.column}`;
        });

        // Track changes to update line/char counts
        inputEditor.onDidChangeModelContent(() => {
            const val = inputEditor.getValue();
            const lines = inputEditor.getModel().getLineCount();
            document.getElementById('inputLineCount').innerText = `${lines} lines (${val.length} chars)`;
        });

        // Track cursor position in output editor
        outputEditor.onDidChangeCursorPosition(e => {
            const pos = e.position;
            document.getElementById('outputPosStatus').innerText = `Ln ${pos.lineNumber}, Col ${pos.column}`;
        });

        // Keyboard Shortcut: Ctrl + Enter to Generate
        inputEditor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter, function () {
            document.getElementById('btnExecuteAction').click();
        });

        // Initial default sample load from backend
        loadSample('schema_customers');
    });
}

// Fetch Sample Schema dynamically from backend API
async function loadSample(sampleKey) {
    if (!sampleKey) return;

    try {
        const response = await fetch(`/Home/GetSampleTemplate?key=${encodeURIComponent(sampleKey)}`);
        if (response.ok) {
            const data = await response.json();
            if (inputEditor && data.sql) {
                inputEditor.setValue(data.sql);
            }

            // Automatically set recommended default requirements
            const actionSelect = document.getElementById('actionSelect');
            const reqInput = document.getElementById('customRequirement');

            if (sampleKey === 'buggy_sql') {
                actionSelect.value = 'debug_sql';
                reqInput.value = 'Fix all syntax and grammatical errors, format properly with semicolons.';
            } else if (sampleKey === 'optimize_query') {
                actionSelect.value = 'optimize_query';
                reqInput.value = 'Optimize joins, avoid non-SARGable WHERE predicates, and suggest indexes.';
            } else if (sampleKey === 'stored_proc_input') {
                actionSelect.value = 'create_sp';
                reqInput.value = 'Generate CRUD stored procedures with TRY...CATCH error handling and transactions.';
            } else {
                actionSelect.value = 'create_sp';
                reqInput.value = 'Generate full CRUD stored procedures with TRY...CATCH and transaction handling.';
            }
        }
    } catch (err) {
        console.error('Failed to load template from server:', err);
    }
}

// Add chip prompt to custom requirement box
function addPromptChip(text) {
    const reqBox = document.getElementById('customRequirement');
    if (reqBox) {
        const current = reqBox.value.trim();
        if (current.length === 0) {
            reqBox.value = text;
        } else if (!current.includes(text)) {
            reqBox.value = `${current}, ${text}`;
        }
        reqBox.focus();
    }
}

// Copy output to clipboard
function copyOutputToClipboard() {
    if (!outputEditor) return;
    const text = outputEditor.getValue();
    navigator.clipboard.writeText(text).then(() => {
        const btn = document.getElementById('btnCopyOutput');
        const originalHtml = btn.innerHTML;
        btn.innerHTML = `<i class="bi bi-check2"></i> Copied!`;
        setTimeout(() => { btn.innerHTML = originalHtml; }, 2000);
    });
}

// Download output as .sql file
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

// Clear input editor
function clearInputEditor() {
    if (inputEditor) {
        inputEditor.setValue('');
        inputEditor.focus();
    }
}

// Clear output editor
function clearOutputEditor() {
    if (outputEditor) {
        outputEditor.setValue('');
    }
}

// Switch layout view mode
function switchLayout(mode) {
    const inputCard = document.getElementById('inputPanelCard');
    const outputCard = document.getElementById('outputPanelCard');
    const grid = document.getElementById('editorsGrid');

    document.querySelectorAll('.view-tab-btn').forEach(btn => btn.classList.remove('active'));
    document.getElementById(`tab-${mode}`).classList.add('active');

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

    if (inputEditor) inputEditor.layout();
    if (outputEditor) outputEditor.layout();
}

// Jump to line in input editor from diagnostics drawer
function jumpToLine(line, col) {
    if (inputEditor && line > 0) {
        inputEditor.revealPositionInCenter({ lineNumber: line, column: col || 1 });
        inputEditor.setPosition({ lineNumber: line, column: col || 1 });
        inputEditor.focus();
    }
}

// Main event initializations
document.addEventListener('DOMContentLoaded', () => {
    initMonaco();

    // Monaco layout trigger on output tab change
    const codeTabBtn = document.getElementById('tab-code-btn');
    if (codeTabBtn) {
        codeTabBtn.addEventListener('shown.bs.tab', () => {
            if (outputEditor) {
                setTimeout(() => outputEditor.layout(), 50);
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

            // Set loading state
            btnExec.disabled = true;
            btnExec.innerHTML = `<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Processing...`;

            try {
                // Call server generation endpoint
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
                    })
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
                        expElem.innerText = data.explanation || 'Processed successfully using T-SQL standards.';
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
                        outputEditor.setValue(`-- Error executing request: Server responded with status ${response.status}`);
                    }
                }
            } catch (err) {
                console.error(err);
                if (outputEditor) {
                    outputEditor.setValue(`-- Request failed: ${err.message}`);
                }
            } finally {
                btnExec.disabled = false;
                btnExec.innerHTML = `<i class="bi bi-lightning-charge-fill"></i> Generate Script`;
            }
        });
    }
});
