// @ts-nocheck
/**
 * Ocean Dashboard - Pattern Detection Module
 * 
 * This module provides functionality for detecting and visualizing patterns in ocean data:
 * - Trend detection: Identifies upward or downward trends in data series
 * - Anomaly detection: Identifies outlier values that deviate significantly from expected values
 * - Cyclical pattern detection: Identifies repeating patterns in the data
 * - Storm event detection: Identifies periods of extreme wave/wind conditions
 * 
 * Key features:
 * - Visual indicators on charts using annotations
 * - Interactive tooltips showing pattern information
 * - Tabbed interface for exploring different pattern types
 * - Export functionality for detected patterns
 */

// Global variable to store detected patterns, storm events, and anomalies
if (!window.patternAnalysisResults) window.patternAnalysisResults = { patterns: [], stormEvents: [], anomalies: [] };

// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Variáveis globais para paginação
if (!window.tableData) window.tableData = [];
if (!window.paginaAtual) window.paginaAtual = 1;
const registosPorPagina = 10;

function renderTabelaPaginada() {
    const tableBody = document.querySelector('table tbody');
    const mensagemSemDados = document.getElementById('mensagemSemDados');
    const totalRegistos = document.getElementById('totalRegistos');
    if (!tableData.length) {
        tableBody.innerHTML = '';
        mensagemSemDados.style.display = 'block';
        totalRegistos.textContent = '0';
        document.getElementById('paginacaoTabela').innerHTML = '';
        return;
    }
    mensagemSemDados.style.display = 'none';
    totalRegistos.textContent = tableData.length;
    // Mostrar todos os registos sem paginação
    tableBody.innerHTML = tableData.map(item => `
        <tr>
            <td>${new Date(item.timestamp).toLocaleString('pt-PT')}</td>
            <td>${item.location || ''}</td>
            <td>${item.waveHeight !== undefined && item.waveHeight !== null ? item.waveHeight.toFixed(2) : 'N/A'}</td>
            <td>${item.wavePeriod !== undefined && item.wavePeriod !== null ? item.wavePeriod.toFixed(2) : 'N/A'}</td>
            <td>${item.waveDirection !== undefined && item.waveDirection !== null ? item.waveDirection.toFixed(2) : 'N/A'}</td>
            <td>${item.seaTemperature !== undefined && item.seaTemperature !== null ? item.seaTemperature.toFixed(2) : 'N/A'}</td>
        </tr>
    `).join('');
    // Esconder paginação
    document.getElementById('paginacaoTabela').innerHTML = '';
}

// Inicialização dos gráficos se não existirem
document.addEventListener('DOMContentLoaded', function() {
    // Inicializar gráficos se não existirem
    if (!window.charts) {
        window.charts = [];
        const chartConfigs = [
            { id: 'waveHeightChart', label: 'Altura das Ondas', color: '#007bff', field: 'wave_height', dataField: 'waveHeight' },
            { id: 'waveDirectionChart', label: 'Direção das Ondas', color: '#17c9e6', field: 'wave_direction', dataField: 'waveDirection' },
            { id: 'temperatureChart', label: 'Temperatura da Água', color: '#198754', field: 'temperature', dataField: 'seaTemperature' },
            { id: 'wavePeriodChart', label: 'Período das Ondas', color: '#ffc107', field: 'wave_period', dataField: 'wavePeriod' }
        ];
        chartConfigs.forEach(cfg => {
            const ctx = document.getElementById(cfg.id)?.getContext('2d');
            if (ctx) {
                window.charts.push(new Chart(ctx, {
                    type: 'line',
                    data: { 
                        labels: [], 
                        datasets: [
                            { 
                                label: cfg.label, 
                                data: [], 
                                borderColor: cfg.color, 
                                backgroundColor: cfg.color + '33', 
                                tension: 0.2 
                            }
                        ] 
                    },
                    options: {
                        responsive: true,
                        plugins: {
                            legend: { display: false },
                            tooltip: { 
                                enabled: true,
                                callbacks: {
                                    afterBody: function(context) {
                                        // Check if there are patterns at this timestamp
                                        return getPatternInfoForTooltip(context[0].label, cfg.field);
                                    }
                                }
                            },
                            title: { display: false },
                            annotation: {
                                annotations: {}
                            }
                        },
                        locale: 'pt-PT',
                        scales: { x: { display: true }, y: { display: true } },
                        dataField: cfg.field  // Store the data field for pattern matching
                    }
                }));
            }
        });
    }
    atualizarDashboard();
    const refreshButton = document.getElementById('refreshData');
    if (refreshButton) {
        refreshButton.addEventListener('click', function() {
            atualizarDashboard();
        });
    }
    const exportBtn = document.getElementById('exportCSV');
    if (exportBtn) {
        exportBtn.addEventListener('click', exportarCSV);
    }
});

// Mensagem amigável nos gráficos quando não há dados
function mostrarMensagemSemDadosNosGraficos() {
    if (window.charts && Array.isArray(window.charts)) {
        window.charts.forEach(chart => {
            chart.data.labels = [];
            chart.data.datasets[0].data = [];
            chart.update();
            const ctx = chart.ctx;
            ctx.save();
            ctx.font = '18px Arial';
            ctx.fillStyle = '#888';
            ctx.textAlign = 'center';
            ctx.fillText('Sem dados para mostrar', chart.width / 2, chart.height / 2);
            ctx.restore();
        });
    }
}

// Função para atualizar os gráficos com novos dados
function atualizarDashboard() {
    const timeRange = document.getElementById('timeRange')?.value || '24h';
    const location = document.getElementById('location')?.value || 'all';
    const resolution = document.getElementById('resolution')?.value || 'hour';

    fetch(`/Home/GetLatestData?timeRange=${timeRange}&location=${encodeURIComponent(location)}&resolution=${resolution}`)
        .then(response => response.json())
        .then(data => {
            window.tableData = data;
            window.paginaAtual = 1;
            renderTabelaPaginada();
            // Atualizar gráficos
            if (!data || data.length === 0) {
                mostrarMensagemSemDadosNosGraficos();
                return;
            }            const timestamps = data.map(d => new Date(d.timestamp).toLocaleString('pt-PT'));
            const waveHeights = data.map(d => d.waveHeight !== null && d.waveHeight !== undefined ? d.waveHeight : 0);
            const waveDirections = data.map(d => d.waveDirection !== null && d.waveDirection !== undefined ? d.waveDirection : 0);            const temperatures = data.map(d => d.seaTemperature !== null && d.seaTemperature !== undefined ? d.seaTemperature : 0); 
            const wavePeriods = data.map(d => d.wavePeriod !== null && d.wavePeriod !== undefined ? d.wavePeriod : 0);
            
            if (window.charts && Array.isArray(window.charts)) {
                window.charts.forEach(chart => {
                    switch (chart.canvas.id) {
                        case 'waveHeightChart':
                            chart.data.labels = timestamps;
                            chart.data.datasets[0].data = waveHeights;
                            break;
                        case 'waveDirectionChart':
                            chart.data.labels = timestamps;
                            chart.data.datasets[0].data = waveDirections;
                            break;
                        case 'temperatureChart':
                            chart.data.labels = timestamps;
                            chart.data.datasets[0].data = temperatures;
                            break;
                        case 'wavePeriodChart':
                            chart.data.labels = timestamps;
                            chart.data.datasets[0].data = wavePeriods;
                            break;
                    }
                    chart.options.locale = 'pt-PT';
                    chart.update();
                });
            }
        })
        .catch(error => {
            console.error('Erro ao atualizar dados:', error);
            document.querySelectorAll('.chart-container').forEach(container => {
                container.innerHTML = '<div class="alert alert-danger">' +
                    '<h5>Erro ao carregar dados</h5>' +
                    '<p>' + error.message + '</p>' +
                    '<p>Verifique a ligação ao servidor.</p>' +
                '</div>';
            });
            const tableBody = document.querySelector('table tbody');
            if (tableBody) {
                tableBody.innerHTML = '<tr><td colspan="6" class="text-center text-danger">Erro ao carregar dados</td></tr>';
            }
        });
}

// Exportação CSV
function exportarCSV() {
    if (!window.tableData.length) return;
    const cabecalho = ['Data/Hora','Localização','Altura (m)','Período (s)','Direção (°)','Temperatura (°C)'];    const linhas = tableData.map(item => [
        new Date(item.timestamp).toLocaleString('pt-PT'),
        item.location,
        item.waveHeight !== undefined && item.waveHeight !== null ? item.waveHeight.toFixed(2) : 'N/A',
        item.wavePeriod !== undefined && item.wavePeriod !== null ? item.wavePeriod.toFixed(2) : 'N/A',
        item.waveDirection !== undefined && item.waveDirection !== null ? item.waveDirection.toFixed(2) : 'N/A',
        item.seaTemperature !== undefined && item.seaTemperature !== null ? item.seaTemperature.toFixed(2) : 'N/A'
    ]);
    let csv = cabecalho.join(';') + '\n';
    csv += linhas.map(l => l.join(';')).join('\n');
    const blob = new Blob([csv], {type: 'text/csv;charset=utf-8;'});
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'registos_oceanicos.csv';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}

// Pattern Detection functionality
document.addEventListener('DOMContentLoaded', function() {
    const detectPatternsBtn = document.getElementById('detectPatterns');
    if (detectPatternsBtn) {
        detectPatternsBtn.addEventListener('click', detectPatterns);
    }
    
    const exportPatternsBtn = document.getElementById('exportPatterns');
    if (exportPatternsBtn) {
        exportPatternsBtn.addEventListener('click', exportPatterns);
    }
});

// Detect patterns from API
function detectPatterns() {
    // Get selected options
    const patternType = document.getElementById('patternType').value;
    const dataField = document.getElementById('dataField').value;
    const timeRange = document.getElementById('timeRange').value;
    const location = document.getElementById('location').value;
    const windowSize = document.getElementById('windowSize').value;
    
    // Show loader, hide results and error
    document.getElementById('patternLoader').classList.remove('d-none');
    document.getElementById('patternResults').style.display = 'none';
    document.getElementById('patternError').classList.add('d-none');
    
    // Build API URL
    const url = `/api/Analytics/detect-patterns?patternType=${patternType}&dataField=${dataField}&timeRange=${timeRange}&location=${location}&windowSize=${windowSize}`;
    
    // Call API
    fetch(url)
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! Status: ${response.status}`);
            }
            return response.json();
        })
        .then(data => {
            document.getElementById('patternLoader').classList.add('d-none');
            
            if (!data.success) {
                showPatternError(data.errorMessage || 'Erro ao detectar padrões');
                return;
            }
            
            displayPatternResults(data);
            visualizePatternsOnCharts(data); // Visualize patterns on charts
        })
        .catch(error => {
            document.getElementById('patternLoader').classList.add('d-none');
            showPatternError(error.message || 'Erro ao comunicar com o servidor');
            console.error('Erro na detecção de padrões:', error);
        });
}

// Display pattern detection results
function displayPatternResults(data) {
    // Show results container
    document.getElementById('patternResults').style.display = 'block';
    
    // Update counts in tabs
    document.getElementById('patternsCount').textContent = data.patterns.length;
    document.getElementById('stormsCount').textContent = data.stormEvents.length;
    document.getElementById('anomaliesCount').textContent = data.anomalies.length;
    
    // Display patterns
    const patternsContainer = document.getElementById('patternsList');
    if (data.patterns.length === 0) {
        document.getElementById('noPatternsMessage').style.display = 'block';
        patternsContainer.innerHTML = '';
    } else {
        document.getElementById('noPatternsMessage').style.display = 'none';
        patternsContainer.innerHTML = data.patterns.map(pattern => {
            const patternTypeClass = pattern.type.includes('tendência') ? 'trend-pattern' : 
                                     pattern.type.includes('cíclico') ? 'cycle-pattern' : '';
            return `
                <div class="list-group-item pattern-item ${patternTypeClass}">
                    <div class="d-flex w-100 justify-content-between">
                        <h5 class="mb-1">${pattern.type}</h5>
                        <small>Confiança: ${pattern.confidenceDisplay}</small>
                    </div>
                    <p class="mb-1">${pattern.description}</p>
                    <div class="d-flex justify-content-between align-items-center">
                        <small>Duração: ${pattern.durationDisplay}</small>
                        <div>Intensidade: ${pattern.intensityBar}</div>
                    </div>
                    <small class="text-muted">Local: ${pattern.location}</small>
                </div>
            `;
        }).join('');
    }
    
    // Display storms
    const stormsContainer = document.getElementById('stormsList');
    if (data.stormEvents.length === 0) {
        document.getElementById('noStormsMessage').style.display = 'block';
        stormsContainer.innerHTML = '';
    } else {
        document.getElementById('noStormsMessage').style.display = 'none';
        stormsContainer.innerHTML = data.stormEvents.map(storm => {
            return `
                <div class="list-group-item storm-item">
                    <div class="d-flex w-100 justify-content-between">
                        <h5 class="mb-1">Tempestade detectada</h5>
                        <small>Severidade: ${storm.severityDisplay}</small>
                    </div>
                    <p class="mb-1">${storm.description}</p>
                    <p class="mb-0">
                        <small class="text-muted">Ondas: ${storm.peakWaveHeight.toFixed(1)}m | </small>
                        <small class="text-muted">Vento: ${storm.peakWindSpeed.toFixed(1)} nós | </small>
                        <small class="text-muted">Rajadas: ${storm.peakGust.toFixed(1)} nós</small>
                    </p>
                    <div class="d-flex justify-content-between align-items-center mt-2">
                        <small>Duração: ${storm.durationDisplay}</small>
                        <small class="text-muted">Local: ${storm.location}</small>
                    </div>
                </div>
            `;
        }).join('');
    }
    
    // Display anomalies
    const anomaliesContainer = document.getElementById('anomaliesList');
    if (data.anomalies.length === 0) {
        document.getElementById('noAnomaliesMessage').style.display = 'block';
        anomaliesContainer.innerHTML = '';
    } else {
        document.getElementById('noAnomaliesMessage').style.display = 'none';
        anomaliesContainer.innerHTML = data.anomalies.map(anomaly => {
            const isSignificant = anomaly.isSignificantAnomaly ? 'significant' : '';
            return `
                <div class="list-group-item anomaly-item ${isSignificant}">
                    <div class="d-flex w-100 justify-content-between">
                        <h5 class="mb-1">Anomalia em ${anomaly.parameter}</h5>
                        <small>Confiança: ${anomaly.confidenceDisplay}</small>
                    </div>
                    <p class="mb-1">
                        Valor esperado: ${anomaly.expectedValue.toFixed(2)} |
                        Valor atual: <strong>${anomaly.actualValue.toFixed(2)}</strong> |
                        Desvio: <strong>${anomaly.deviationDisplay}</strong>
                    </p>
                    <div class="d-flex justify-content-between mt-1">
                        <small class="text-muted">Data: ${new Date(anomaly.timestamp).toLocaleString('pt-PT')}</small>
                        <small class="text-muted">Local: ${anomaly.location}</small>
                    </div>
                </div>
            `;
        }).join('');
    }
}

// Show error message
function showPatternError(message) {
    const errorEl = document.getElementById('patternError');
    errorEl.classList.remove('d-none');
    document.getElementById('patternErrorMessage').textContent = message;
}

// Function to add pattern annotations to charts
function visualizePatternsOnCharts(data) {
    // Store pattern results globally
    window.patternAnalysisResults = data;
    
    // Skip if no charts or no pattern data
    if (!window.charts || !Array.isArray(window.charts) || !data) return;
    
    // For each chart, add annotations based on patterns
    window.charts.forEach(chart => {
        // Clear existing annotations
        chart.options.plugins.annotation.annotations = {};
        
        // Get the data field associated with this chart
        const dataField = chart.options.dataField;
        if (!dataField) return;
        
        // Add trend patterns as background boxes
        if (data.patterns && data.patterns.length > 0) {
            data.patterns.filter(p => p.type.includes('tendência')).forEach((pattern, index) => {
                // Only show patterns relevant to this chart's data field
                if (!pattern.description.toLowerCase().includes(dataField)) return;
                
                const startIndex = findTimestampIndex(chart.data.labels, new Date(pattern.startTime));
                const endIndex = findTimestampIndex(chart.data.labels, new Date(pattern.endTime));
                
                if (startIndex > -1 && endIndex > -1) {
                    chart.options.plugins.annotation.annotations[`trend${index}`] = {
                        type: 'box',
                        xMin: startIndex - 0.5,
                        xMax: endIndex + 0.5,
                        yMin: 'min',
                        yMax: 'max',
                        backgroundColor: pattern.type.includes('crescente') ? 'rgba(40, 167, 69, 0.15)' : 'rgba(220, 53, 69, 0.15)',
                        borderColor: 'transparent'
                    };
                }
            });
        }
        
        // Add anomaly points as point annotations
        if (data.anomalies && data.anomalies.length > 0) {
            data.anomalies.forEach((anomaly, index) => {
                // Only show anomalies relevant to this chart's data field
                if (!anomaly.parameter.toLowerCase().includes(dataField)) return;
                
                const anomalyIndex = findTimestampIndex(chart.data.labels, new Date(anomaly.timestamp));
                
                if (anomalyIndex > -1) {
                    chart.options.plugins.annotation.annotations[`anomaly${index}`] = {
                        type: 'point',
                        xValue: anomalyIndex,
                        yValue: anomaly.actualValue,
                        backgroundColor: 'rgba(220, 53, 69, 0.8)',
                        borderColor: 'rgba(220, 53, 69, 1)',
                        borderWidth: 2,
                        radius: 5,
                        hoverRadius: 8
                    };
                }
            });
        }
        
        // Add storm events as vertical line annotations
        if (data.stormEvents && data.stormEvents.length > 0) {
            data.stormEvents.forEach((storm, index) => {
                // Storm events affect wave height primarily
                if (dataField !== 'wave_height') return;
                
                const startIndex = findTimestampIndex(chart.data.labels, new Date(storm.startTime));
                const endIndex = findTimestampIndex(chart.data.labels, new Date(storm.endTime));
                
                if (startIndex > -1) {
                    // Start line
                    chart.options.plugins.annotation.annotations[`stormStart${index}`] = {
                        type: 'line',
                        xMin: startIndex,
                        xMax: startIndex,
                        yMin: 'min',
                        yMax: 'max',
                        borderColor: 'rgba(255, 193, 7, 0.8)',
                        borderWidth: 2,
                        borderDash: [5, 5],
                        label: {
                            display: true,
                            content: `Tempestade ${storm.severity}/5`,
                            position: 'start'
                        }
                    };
                    
                    // End line
                    if (endIndex > -1) {
                        chart.options.plugins.annotation.annotations[`stormEnd${index}`] = {
                            type: 'line',
                            xMin: endIndex,
                            xMax: endIndex,
                            yMin: 'min',
                            yMax: 'max',
                            borderColor: 'rgba(255, 193, 7, 0.8)',
                            borderWidth: 2,
                            borderDash: [5, 5]
                        };
                    }
                }
            });
        }
        
        // Update chart
        chart.update();
    });
}

// Helper function to find the index of a timestamp in chart labels
function findTimestampIndex(labels, targetDate) {
    const targetTimeStr = targetDate.toLocaleString('pt-PT');
    return labels.findIndex(label => {
        // Match by string comparison of formatted dates
        return label === targetTimeStr;
    });
}

// Helper function to get pattern information for chart tooltips
function getPatternInfoForTooltip(timestamp, dataField) {
    if (!window.patternAnalysisResults) return '';
    
    const lines = [];
    const date = new Date(timestamp);
    
    // Check for patterns
    window.patternAnalysisResults.patterns?.forEach(pattern => {
        if (!pattern.description.toLowerCase().includes(dataField)) return;
        
        const start = new Date(pattern.startTime);
        const end = new Date(pattern.endTime);
        
        if (date >= start && date <= end) {
            lines.push(`📈 ${pattern.type} (${Math.round(pattern.confidence * 100)}% confiança)`);
        }
    });
    
    // Check for anomalies
    window.patternAnalysisResults.anomalies?.forEach(anomaly => {
        if (!anomaly.parameter.toLowerCase().includes(dataField)) return;
        
        const anomalyTime = new Date(anomaly.timestamp);
        
        // Approximate match - within a reasonable timeframe
        if (Math.abs(date - anomalyTime) < 1000 * 60 * 30) { // Within 30 minutes
            lines.push(`⚠️ Anomalia: ${anomaly.deviationDisplay} de desvio`);
        }
    });
    
    // Check for storm events (only for wave height)
    if (dataField === 'wave_height') {
        window.patternAnalysisResults.stormEvents?.forEach(storm => {
            const start = new Date(storm.startTime);
            const end = new Date(storm.endTime);
            
            if (date >= start && date <= end) {
                lines.push(`🌊 Tempestade: Severidade ${storm.severity}/5`);
            }
        });
    }
    
    return lines.length > 0 ? lines.join('\n') : '';
}

// Function to export detected patterns to CSV
function exportPatterns() {
    // Check if we have pattern results
    if (!window.patternAnalysisResults || 
        (!window.patternAnalysisResults.patterns?.length && 
         !window.patternAnalysisResults.stormEvents?.length && 
         !window.patternAnalysisResults.anomalies?.length)) {
        alert('Nenhum padrão disponível para exportar. Execute a detecção de padrões primeiro.');
        return;
    }
    
    // Create CSV content
    let csv = '';
    const data = window.patternAnalysisResults;
    
    // Add patterns
    if (data.patterns?.length > 0) {
        csv += 'PADRÕES DETECTADOS\n';
        csv += 'Tipo;Descrição;Confiança;Data Início;Data Fim;Intensidade;Localização\n';
        data.patterns.forEach(pattern => {
            csv += [
                pattern.type,
                pattern.description,
                (pattern.confidence * 100).toFixed(1) + '%',
                new Date(pattern.startTime).toLocaleString('pt-PT'),
                new Date(pattern.endTime).toLocaleString('pt-PT'),
                pattern.intensity.toFixed(2),
                pattern.location
            ].join(';') + '\n';
        });
        csv += '\n';
    }
    
    // Add storm events
    if (data.stormEvents?.length > 0) {
        csv += 'EVENTOS DE TEMPESTADE\n';
        csv += 'Data Início;Data Fim;Altura Máxima;Vento Máximo;Rajada Máxima;Localização;Severidade;Descrição\n';
        data.stormEvents.forEach(storm => {
            csv += [
                new Date(storm.startTime).toLocaleString('pt-PT'),
                new Date(storm.endTime).toLocaleString('pt-PT'),
                storm.peakWaveHeight.toFixed(2),
                storm.peakWindSpeed.toFixed(2),
                storm.peakGust.toFixed(2),
                storm.location,
                storm.severity,
                storm.description
            ].join(';') + '\n';
        });
        csv += '\n';
    }
    
    // Add anomalies
    if (data.anomalies?.length > 0) {
        csv += 'ANOMALIAS\n';
        csv += 'Data;Parâmetro;Valor Esperado;Valor Atual;Desvio (%);Localização;Confiança\n';
        data.anomalies.forEach(anomaly => {
            csv += [
                new Date(anomaly.timestamp).toLocaleString('pt-PT'),
                anomaly.parameter,
                anomaly.expectedValue.toFixed(2),
                anomaly.actualValue.toFixed(2),
                anomaly.deviationPercent.toFixed(2),
                anomaly.location,
                anomaly.confidence + '%'
            ].join(';') + '\n';
        });
    }
    
    // Create and download the file
    const blob = new Blob([csv], {type: 'text/csv;charset=utf-8;'});
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    
    // Get the current date for the filename
    const now = new Date();
    const dateStr = now.toISOString().split('T')[0];
    a.download = `padroes_oceanicos_${dateStr}.csv`;
    
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}