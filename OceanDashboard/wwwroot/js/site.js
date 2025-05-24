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
    const paginacaoTabela = document.getElementById('paginacaoTabela');
    
    if (!tableData.length) {
        if (tableBody) tableBody.innerHTML = '';
        if (mensagemSemDados) mensagemSemDados.style.display = 'block';
        if (totalRegistos) totalRegistos.textContent = '0';
        if (paginacaoTabela) paginacaoTabela.innerHTML = '';
        return;
    }
    if (mensagemSemDados) mensagemSemDados.style.display = 'none';
    if (totalRegistos) totalRegistos.textContent = tableData.length;    // Mostrar todos os registos sem paginação
    if (tableBody) {
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
    }
    // Esconder paginação
    if (paginacaoTabela) paginacaoTabela.innerHTML = '';
}

// Inicialização dos gráficos se não existirem
document.addEventListener('DOMContentLoaded', function() {
    // Inicializar gráficos se não existirem
    if (!window.charts) {
        window.charts = [];        const chartConfigs = [
            { id: 'waveHeightChart', label: 'Altura das Ondas', color: '#007bff', field: 'wave_height', dataField: 'waveHeight' },
            { id: 'windDirectionChart', label: 'Direção do Vento', color: '#17c9e6', field: 'wind_direction', dataField: 'waveDirection' },
            { id: 'temperatureChart', label: 'Temperatura da Água', color: '#198754', field: 'temperature', dataField: 'seaTemperature' },
            { id: 'windSpeedChart', label: 'Velocidade do Vento', color: '#ffc107', field: 'wind_speed', dataField: 'windSpeed' }
        ];        chartConfigs.forEach(cfg => {
            console.log('Initializing chart:', cfg.id);
            const element = document.getElementById(cfg.id);
            
            if (!element) {
                console.error('Canvas element not found:', cfg.id);
                return;
            }
            
            const ctx = element.getContext('2d');
            if (!ctx) {
                console.error('Could not get 2d context for:', cfg.id);
                return;
            }
            
            try {
                const chart = new Chart(ctx, {
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
                        maintainAspectRatio: false,
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
                        scales: { x: { display: true }, y: { display: true, beginAtZero: false } },
                        dataField: cfg.field  // Store the data field for pattern matching
                    }
                });
                
                window.charts.push(chart);
                console.log('Chart created successfully:', cfg.id);
                
            } catch (error) {
                console.error('Error creating chart', cfg.id, ':', error);
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
    
    // Auto-refresh functionality
    initAutoRefresh();
});

// Auto-refresh variables and functions
let autoRefreshInterval = null;
let autoRefreshCountdown = null;
let autoRefreshTimeLeft = 0;

function initAutoRefresh() {
    const autoRefreshToggle = document.getElementById('autoRefreshToggle');
    const refreshInterval = document.getElementById('refreshInterval');
    const autoRefreshStatus = document.getElementById('autoRefreshStatus');
    const refreshCountdown = document.getElementById('refreshCountdown');
    const lastUpdateTime = document.getElementById('lastUpdateTime');
    
    if (!autoRefreshToggle || !refreshInterval || !autoRefreshStatus) {
        return; // Elements not found, skip auto-refresh setup
    }
    
    // Update last update time
    updateLastUpdateTime();
    
    // Toggle auto-refresh
    autoRefreshToggle.addEventListener('change', function() {
        if (this.checked) {
            startAutoRefresh();
        } else {
            stopAutoRefresh();
        }
    });
    
    // Change interval
    refreshInterval.addEventListener('change', function() {
        if (autoRefreshToggle.checked) {
            stopAutoRefresh();
            startAutoRefresh();
        }
    });
      function startAutoRefresh() {
        const intervalSeconds = parseInt(refreshInterval.value);
        autoRefreshTimeLeft = intervalSeconds;
        
        if (autoRefreshStatus) {
            autoRefreshStatus.textContent = `Ativa (${intervalSeconds}s)`;
            autoRefreshStatus.className = 'text-success fw-medium';
        }
        if (refreshCountdown) refreshCountdown.style.display = 'inline';
          // Start countdown
        autoRefreshCountdown = setInterval(function() {
            autoRefreshTimeLeft--;
            if (refreshCountdown) refreshCountdown.textContent = autoRefreshTimeLeft + 's';
            
            if (autoRefreshTimeLeft <= 0) {
                autoRefreshTimeLeft = intervalSeconds;
            }
        }, 1000);
          // Start auto-refresh
        autoRefreshInterval = setInterval(function() {
            console.log('Auto-refreshing dashboard data...');
            atualizarDashboardWithConnectionCheck();
            autoRefreshTimeLeft = intervalSeconds; // Reset countdown
        }, intervalSeconds * 1000);
        
        // Initial countdown display
        if (refreshCountdown) refreshCountdown.textContent = autoRefreshTimeLeft + 's';
    }
      function stopAutoRefresh() {
        if (autoRefreshInterval) {
            clearInterval(autoRefreshInterval);
            autoRefreshInterval = null;
        }
        
        if (autoRefreshCountdown) {
            clearInterval(autoRefreshCountdown);
            autoRefreshCountdown = null;
        }
        
        if (autoRefreshStatus) {
            autoRefreshStatus.textContent = 'Desativada';
            autoRefreshStatus.className = 'text-muted';
        }
        if (refreshCountdown) refreshCountdown.style.display = 'none';
    }
}

function updateLastUpdateTime() {
    const lastUpdateTime = document.getElementById('lastUpdateTime');
    if (lastUpdateTime) {
        const now = new Date();
        lastUpdateTime.textContent = now.toLocaleTimeString('pt-PT');
    }
}

function showLoadingIndicator(show) {
    const refreshButton = document.getElementById('refreshData');
    const autoRefreshStatus = document.getElementById('autoRefreshStatus');
    
    if (show) {
        if (refreshButton) {
            refreshButton.innerHTML = '<i class="bi bi-arrow-clockwise spin"></i> Carregando...';
            refreshButton.disabled = true;
        }
        
        if (autoRefreshStatus && autoRefreshStatus.textContent !== 'Desativada') {
            autoRefreshStatus.innerHTML = '<i class="bi bi-arrow-clockwise spin"></i> Atualizando...';
        }
    } else {
        if (refreshButton) {
            refreshButton.innerHTML = '<i class="bi bi-arrow-clockwise"></i> Atualizar Agora';
            refreshButton.disabled = false;
        }
        
        const autoRefreshToggle = document.getElementById('autoRefreshToggle');
        const refreshInterval = document.getElementById('refreshInterval');
        
        if (autoRefreshStatus && autoRefreshToggle?.checked) {
            const intervalSeconds = parseInt(refreshInterval?.value || 30);
            autoRefreshStatus.innerHTML = `Ativa (${intervalSeconds}s)`;
            autoRefreshStatus.className = 'text-success fw-medium';
        } else if (autoRefreshStatus) {
            autoRefreshStatus.textContent = 'Desativada';
            autoRefreshStatus.className = 'text-muted';
        }
    }
}

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
    console.log('Updating dashboard...');
    console.log('Charts available:', window.charts?.length || 0);
    
    const timeRange = document.getElementById('timeRange')?.value || '24h';
    const location = document.getElementById('location')?.value || 'all';
    const resolution = document.getElementById('resolution')?.value || 'hour';
    
    // Show loading indicator
    showLoadingIndicator(true);

    fetch(`/Home/GetLatestData?timeRange=${timeRange}&location=${encodeURIComponent(location)}&resolution=${resolution}`)
        .then(response => response.json())        .then(data => {
            console.log('Data received:', data.length, 'records');
            
            // Ensure charts are initialized before updating
            ensureChartsInitialized();
            
            window.tableData = data;
            window.paginaAtual = 1;
            renderTabelaPaginada();
            // Atualizar gráficos
            if (!data || data.length === 0) {
                mostrarMensagemSemDadosNosGraficos();
                return;
            }            const timestamps = data.map(d => new Date(d.timestamp).toLocaleString('pt-PT'));
            const waveHeights = data.map(d => d.waveHeight !== null && d.waveHeight !== undefined ? d.waveHeight : 0);
            const waveDirections = data.map(d => d.waveDirection !== null && d.waveDirection !== undefined ? d.waveDirection : 0);            
            const temperatures = data.map(d => d.seaTemperature !== null && d.seaTemperature !== undefined ? d.seaTemperature : 0); 
            const windSpeeds = data.map(d => d.windSpeed !== null && d.windSpeed !== undefined ? d.windSpeed : 0);
              if (window.charts && Array.isArray(window.charts)) {
                console.log('Updating', window.charts.length, 'charts with', data.length, 'data points');
                
                window.charts.forEach((chart, index) => {
                    console.log(`Updating chart ${index + 1}:`, chart.canvas.id);
                    
                    switch (chart.canvas.id) {
                        case 'waveHeightChart':
                            chart.data.labels = timestamps;
                            chart.data.datasets[0].data = waveHeights;
                            console.log('Wave heights:', waveHeights.slice(0, 5), '...');
                            break;
                        case 'windDirectionChart':
                            chart.data.labels = timestamps;
                            chart.data.datasets[0].data = waveDirections;
                            console.log('Wave directions:', waveDirections.slice(0, 5), '...');
                            break;
                        case 'temperatureChart':
                            chart.data.labels = timestamps;
                            chart.data.datasets[0].data = temperatures;
                            console.log('Temperatures:', temperatures.slice(0, 5), '...');
                            break;
                        case 'windSpeedChart':
                            chart.data.labels = timestamps;
                            chart.data.datasets[0].data = windSpeeds;
                            console.log('Wind speeds:', windSpeeds.slice(0, 5), '...');
                            break;
                    }
                    chart.options.locale = 'pt-PT';
                    chart.update();                
                });
            } else {
                console.error('No charts available for update');
            }// Update last refresh time
            updateLastUpdateTime();
            
            // Hide loading indicator
            showLoadingIndicator(false);
            
            // Show success notification only for manual refresh (not auto-refresh)
            const autoRefreshToggle = document.getElementById('autoRefreshToggle');
            if (!autoRefreshToggle?.checked) {
                showNotification(`Dashboard atualizado com ${data.length} registos`, 'success', 2000);
            }
        })
        .catch(error => {
            console.error('Erro ao atualizar dados:', error);
            
            // Hide loading indicator
            showLoadingIndicator(false);
            
            // Show error notification
            showNotification('Erro ao carregar dados do servidor', 'danger', 5000);
            
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
    const patternLoader = document.getElementById('patternLoader');
    const patternResults = document.getElementById('patternResults');
    const patternError = document.getElementById('patternError');
    
    if (patternLoader) patternLoader.classList.remove('d-none');
    if (patternResults) patternResults.style.display = 'none';
    if (patternError) patternError.classList.add('d-none');
    
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
    const patternResults = document.getElementById('patternResults');
    if (patternResults) patternResults.style.display = 'block';
    
    // Update counts in tabs
    const patternsCount = document.getElementById('patternsCount');
    const stormsCount = document.getElementById('stormsCount');
    const anomaliesCount = document.getElementById('anomaliesCount');
    
    if (patternsCount) patternsCount.textContent = data.patterns.length;
    if (stormsCount) stormsCount.textContent = data.stormEvents.length;
    if (anomaliesCount) anomaliesCount.textContent = data.anomalies.length;
    
    // Display patterns
    const patternsContainer = document.getElementById('patternsList');
    const noPatternsMessage = document.getElementById('noPatternsMessage');
    
    if (data.patterns.length === 0) {
        if (noPatternsMessage) noPatternsMessage.style.display = 'block';
        if (patternsContainer) patternsContainer.innerHTML = '';    } else {
        if (noPatternsMessage) noPatternsMessage.style.display = 'none';
        if (patternsContainer) patternsContainer.innerHTML = data.patterns.map(pattern => {
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
    const noStormsMessage = document.getElementById('noStormsMessage');
    
    if (data.stormEvents.length === 0) {
        if (noStormsMessage) noStormsMessage.style.display = 'block';
        if (stormsContainer) stormsContainer.innerHTML = '';
    } else {
        if (noStormsMessage) noStormsMessage.style.display = 'none';
        if (stormsContainer) stormsContainer.innerHTML = data.stormEvents.map(storm => {
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

// Função para forçar inicialização de gráficos se necessário
function ensureChartsInitialized() {
    if (!window.charts || window.charts.length === 0) {
        console.log('Charts not initialized, forcing initialization...');
        
        const chartConfigs = [
            { id: 'waveHeightChart', label: 'Altura das Ondas', color: '#007bff', field: 'wave_height', dataField: 'waveHeight' },
            { id: 'windDirectionChart', label: 'Direção do Vento', color: '#17c9e6', field: 'wind_direction', dataField: 'waveDirection' },
            { id: 'temperatureChart', label: 'Temperatura da Água', color: '#198754', field: 'temperature', dataField: 'seaTemperature' },
            { id: 'windSpeedChart', label: 'Velocidade do Vento', color: '#ffc107', field: 'wind_speed', dataField: 'windSpeed' }
        ];
        
        window.charts = [];
        
        chartConfigs.forEach(cfg => {
            console.log('Force initializing chart:', cfg.id);
            const element = document.getElementById(cfg.id);
            
            if (!element) {
                console.error('Canvas element not found:', cfg.id);
                return;
            }
            
            const ctx = element.getContext('2d');
            if (!ctx) {
                console.error('Could not get 2d context for:', cfg.id);
                return;
            }
            
            try {
                const chart = new Chart(ctx, {
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
                        maintainAspectRatio: false,
                        plugins: {
                            legend: { display: false },
                            tooltip: { 
                                enabled: true,
                                callbacks: {
                                    afterBody: function(context) {
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
                        scales: { x: { display: true }, y: { display: true, beginAtZero: false } },
                        dataField: cfg.field
                    }
                });
                
                window.charts.push(chart);
                console.log('Chart force-created successfully:', cfg.id);
                
            } catch (error) {
                console.error('Error force-creating chart', cfg.id, ':', error);
            }
        });
    }
}

// Enhanced table functionality
function refreshTableData() {
    const tableLastUpdate = document.getElementById('tableLastUpdate');
    if (tableLastUpdate) {
        tableLastUpdate.textContent = new Date().toLocaleTimeString('pt-PT');
    }
    
    // Refresh the entire dashboard data
    atualizarDashboard();
    
    showNotification('Dados da tabela atualizados', 'success', 2000);
}

// Table search functionality
function initTableSearch() {
    const searchInput = document.getElementById('tableSearch');
    const tableBody = document.getElementById('dataTableBody');
    
    if (!searchInput || !tableBody) return;
    
    searchInput.addEventListener('input', function() {
        const searchTerm = this.value.toLowerCase();
        const rows = tableBody.querySelectorAll('tr.data-row');
        
        rows.forEach(row => {
            const text = row.textContent.toLowerCase();
            if (text.includes(searchTerm)) {
                row.style.display = '';
            } else {
                row.style.display = 'none';
            }
        });
    });
}

// Table sorting functionality
function initTableSorting() {
    const sortHeaders = document.querySelectorAll('.sort-header');
    
    sortHeaders.forEach(header => {
        header.addEventListener('click', function() {
            const sortType = this.dataset.sort;
            const tableBody = document.getElementById('dataTableBody');
            const rows = Array.from(tableBody.querySelectorAll('tr.data-row'));
            
            // Remove sorted class from all headers
            sortHeaders.forEach(h => h.classList.remove('sorted'));
            this.classList.add('sorted');
            
            // Determine sort direction
            const isAscending = !this.classList.contains('desc');
            this.classList.toggle('desc');
            
            // Sort rows
            rows.sort((a, b) => {
                let aVal, bVal;
                
                switch(sortType) {
                    case 'timestamp':
                        aVal = new Date(a.children[0].textContent.trim());
                        bVal = new Date(b.children[0].textContent.trim());
                        break;
                    case 'station':
                        aVal = a.children[1].textContent.trim();
                        bVal = b.children[1].textContent.trim();
                        break;
                    case 'sensor':
                        aVal = a.children[2].textContent.trim();
                        bVal = b.children[2].textContent.trim();
                        break;
                    case 'wave-height':
                        aVal = parseFloat(a.children[3].textContent.trim());
                        bVal = parseFloat(b.children[3].textContent.trim());
                        break;
                    case 'wave-period':
                        aVal = parseFloat(a.children[4].textContent.trim());
                        bVal = parseFloat(b.children[4].textContent.trim());
                        break;
                    case 'wind-speed':
                        aVal = parseFloat(a.children[5].textContent.trim());
                        bVal = parseFloat(b.children[5].textContent.trim());
                        break;
                    case 'wind-direction':
                        aVal = parseFloat(a.children[6].textContent.trim());
                        bVal = parseFloat(b.children[6].textContent.trim());
                        break;
                    case 'temperature':
                        aVal = parseFloat(a.children[7].textContent.trim());
                        bVal = parseFloat(b.children[7].textContent.trim());
                        break;
                    default:
                        return 0;
                }
                
                if (aVal < bVal) return isAscending ? -1 : 1;
                if (aVal > bVal) return isAscending ? 1 : -1;
                return 0;
            });
            
            // Re-append sorted rows
            rows.forEach(row => tableBody.appendChild(row));
        });
    });
}

// Chart error handling and retry mechanism
function handleChartError(chartId, error) {
    console.error(`Error in chart ${chartId}:`, error);
    
    const chartContainer = document.querySelector(`#${chartId}`).parentElement;
    if (chartContainer) {
        chartContainer.innerHTML = `
            <div class="chart-error">
                <i class="bi bi-exclamation-triangle text-warning" style="font-size: 3rem;"></i>
                <h5 class="mt-3 mb-2">Erro ao carregar gráfico</h5>
                <p class="text-muted mb-3">${error.message || 'Erro desconhecido'}</p>
                <button class="btn btn-outline-primary btn-sm" onclick="retryChart('${chartId}')">
                    <i class="bi bi-arrow-clockwise me-1"></i>Tentar novamente
                </button>
            </div>
        `;
    }
}

// Retry chart initialization
function retryChart(chartId) {
    const chartContainer = document.querySelector(`#${chartId}`).parentElement;
    if (chartContainer) {
        chartContainer.innerHTML = `
            <div class="chart-loading">
                <div class="spinner-border text-primary me-2" role="status"></div>
                <span>Carregando gráfico...</span>
            </div>
        `;
        
        // Re-create canvas element
        setTimeout(() => {
            chartContainer.innerHTML = `<canvas id="${chartId}"></canvas>`;
            
            // Find and reinitialize the specific chart
            const chartConfig = window.chartConfigs?.find(cfg => cfg.id === chartId);
            if (chartConfig) {
                initSingleChart(chartConfig);
            }
        }, 1000);
    }
}

// Initialize single chart with error handling
function initSingleChart(config) {
    try {
        const ctx = document.getElementById(config.id)?.getContext('2d');
        if (!ctx) {
            throw new Error(`Canvas element not found: ${config.id}`);
        }
        
        const chart = new Chart(ctx, {
            type: 'line',
            data: { 
                labels: [], 
                datasets: [
                    { 
                        label: config.label, 
                        data: [], 
                        borderColor: config.color, 
                        backgroundColor: config.color + '33', 
                        tension: 0.2,
                        fill: false,
                        pointRadius: 2,
                        pointHoverRadius: 5
                    }
                ] 
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: true, position: 'top' },
                    tooltip: { 
                        enabled: true,
                        mode: 'index',
                        intersect: false,
                        callbacks: {
                            afterBody: function(context) {
                                return getPatternInfoForTooltip(context[0].label, config.field);
                            }
                        }
                    },
                    title: { display: false },
                    annotation: {
                        annotations: {}
                    }
                },
                locale: 'pt-PT',
                scales: { 
                    x: { 
                        display: true,
                        grid: {
                            display: true,
                            color: 'rgba(0,0,0,0.1)'
                        }
                    }, 
                    y: { 
                        display: true,
                        grid: {
                            display: true,
                            color: 'rgba(0,0,0,0.1)'
                        },
                        beginAtZero: false
                    } 
                },
                dataField: config.field,
                interaction: {
                    mode: 'index',
                    intersect: false
                }
            }
        });
        
        // Add to global charts array
        if (!window.charts) window.charts = [];
        window.charts.push(chart);
        
        return chart;
    } catch (error) {
        handleChartError(config.id, error);
        return null;
    }
}

// Enhanced chart initialization with loading states
function initChartsWithLoadingStates() {    const chartConfigs = [
        { id: 'waveHeightChart', label: 'Altura das Ondas (m)', color: '#007bff', field: 'wave_height', dataField: 'waveHeight' },
        { id: 'windDirectionChart', label: 'Direção do Vento (°)', color: '#17c9e6', field: 'wind_direction', dataField: 'waveDirection' },
        { id: 'temperatureChart', label: 'Temperatura da Água (°C)', color: '#198754', field: 'temperature', dataField: 'seaTemperature' },
        { id: 'windSpeedChart', label: 'Velocidade do Vento (nós)', color: '#ffc107', field: 'wind_speed', dataField: 'windSpeed' }
    ];
    
    // Store configs globally for retry functionality
    window.chartConfigs = chartConfigs;
    
    chartConfigs.forEach(config => {
        const chartContainer = document.querySelector(`#${config.id}`)?.parentElement;
        if (chartContainer) {
            // Show loading state
            chartContainer.innerHTML = `
                <div class="chart-loading">
                    <div class="spinner-border text-primary me-2" role="status"></div>
                    <span>Carregando ${config.label.toLowerCase()}...</span>
                </div>
            `;
            
            // Initialize chart after brief delay
            setTimeout(() => {
                chartContainer.innerHTML = `<canvas id="${config.id}"></canvas>`;
                initSingleChart(config);
            }, 500);
        }
    });
}

// Enhanced error handling and connection status
function checkServerConnection() {
    return fetch('/Home/GetLatestData?timeRange=1h&location=all&resolution=hour', {
        method: 'HEAD', // Just check if endpoint is reachable
        cache: 'no-cache'
    })
    .then(response => response.ok)
    .catch(() => false);
}

function showConnectionStatus(isConnected) {
    const autoRefreshStatus = document.getElementById('autoRefreshStatus');
    const autoRefreshToggle = document.getElementById('autoRefreshToggle');
    
    if (!isConnected) {
        if (autoRefreshStatus) {
            autoRefreshStatus.innerHTML = '<i class="bi bi-exclamation-triangle text-warning"></i> Sem conexão';
            autoRefreshStatus.className = 'text-warning fw-medium';
        }
        
        // Disable auto-refresh if connection is lost
        if (autoRefreshToggle && autoRefreshToggle.checked) {
            console.warn('Connection lost - disabling auto-refresh');
            autoRefreshToggle.checked = false;
            autoRefreshToggle.dispatchEvent(new Event('change'));
        }
    }
}

// Enhanced dashboard update with connection checking
function atualizarDashboardWithConnectionCheck() {
    checkServerConnection().then(isConnected => {
        if (isConnected) {
            atualizarDashboard();
        } else {
            showConnectionStatus(false);
            console.error('Server connection failed - skipping refresh');
        }
    });
}

// Notification system for dashboard updates
function showNotification(message, type = 'info', duration = 3000) {
    // Remove any existing notifications
    const existingNotification = document.querySelector('.dashboard-notification');
    if (existingNotification) {
        existingNotification.remove();
    }
    
    // Create notification element
    const notification = document.createElement('div');
    notification.className = `alert alert-${type} dashboard-notification position-fixed`;
    notification.style.cssText = `
        top: 20px;
        right: 20px;
        z-index: 9999;
        min-width: 300px;
        opacity: 0;
        transition: opacity 0.3s ease-in-out;
    `;
    notification.innerHTML = `
        <div class="d-flex align-items-center">
            <i class="bi bi-${type === 'success' ? 'check-circle' : type === 'warning' ? 'exclamation-triangle' : type === 'danger' ? 'x-circle' : 'info-circle'} me-2"></i>
            <span>${message}</span>
            <button type="button" class="btn-close ms-auto" aria-label="Close"></button>
        </div>
    `;
    
    // Add to page
    document.body.appendChild(notification);
    
    // Show notification
    setTimeout(() => {
        notification.style.opacity = '1';
    }, 100);
    
    // Auto-hide after duration
    setTimeout(() => {
        notification.style.opacity = '0';
        setTimeout(() => {
            if (notification.parentNode) {
                notification.remove();
            }
        }, 300);
    }, duration);
    
    // Manual close button
    notification.querySelector('.btn-close').addEventListener('click', () => {
        notification.style.opacity = '0';
        setTimeout(() => {
            if (notification.parentNode) {
                notification.remove();
            }
        }, 300);
    });
}