// Analysis Dashboard JavaScript
console.log('🔬 Analysis Dashboard initializing...');

let analysisPresets = [];
let currentAnalysis = null;

document.addEventListener('DOMContentLoaded', function() {
    initializeAnalysisDashboard();
});

function initializeAnalysisDashboard() {
    console.log('Initializing Analysis Dashboard...');
    
    // Set default dates
    setDefaultDates();
    
    // Load analysis presets
    loadAnalysisPresets();
    
    // Setup event listeners
    setupEventListeners();
    
    // Load available stations
    loadAvailableStations();
}

function setDefaultDates() {
    const endDate = new Date();
    const startDate = new Date();
    startDate.setDate(startDate.getDate() - 7); // Default to last 7 days
    
    document.getElementById('startDate').value = formatDateTimeLocal(startDate);
    document.getElementById('endDate').value = formatDateTimeLocal(endDate);
}

function formatDateTimeLocal(date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    
    return `${year}-${month}-${day}T${hours}:${minutes}`;
}

function loadAnalysisPresets() {
    fetch('/Home/GetAnalysisPresets')
        .then(response => response.json())
        .then(presets => {
            analysisPresets = presets;
            renderAnalysisPresets(presets);
        })
        .catch(error => {
            console.error('Error loading analysis presets:', error);
            showErrorMessage('Erro ao carregar presets de análise');
        });
}

function renderAnalysisPresets(presets) {
    const container = document.getElementById('analysisPresets');
    if (!container) return;
    
    container.innerHTML = presets.map(preset => `
        <div class="col-md-2 col-sm-4">
            <div class="card preset-card h-100" onclick="applyPreset('${preset.name}')">
                <div class="card-body text-center p-3">
                    <div class="preset-icon mb-2">
                        <i class="bi bi-graph-up text-primary" style="font-size: 2rem;"></i>
                    </div>
                    <h6 class="card-title mb-1">${preset.name}</h6>
                    <small class="text-muted">Análise especializada</small>
                </div>
            </div>
        </div>
    `).join('');
}

function applyPreset(presetName) {
    const preset = analysisPresets.find(p => p.name === presetName);
    if (!preset) return;
    
    // Clear current form
    document.getElementById('analysisForm').reset();
    setDefaultDates();
    
    // Apply preset filters
    const filters = preset.filters;
    Object.keys(filters).forEach(key => {
        const input = document.querySelector(`[name="${key}"]`);
        if (input) {
            input.value = filters[key];
        }
    });
    
    // Highlight selected preset
    document.querySelectorAll('.preset-card').forEach(card => {
        card.classList.remove('active');
    });
    event.target.closest('.preset-card').classList.add('active');
    
    // Show success message
    showSuccessMessage(`Preset "${presetName}" aplicado com sucesso!`);
}

function setupEventListeners() {
    // Form submission
    document.getElementById('analysisForm').addEventListener('submit', function(e) {
        e.preventDefault();
        executeAnalysis();
    });
    
    // Clear filters button
    document.getElementById('clearFiltersBtn').addEventListener('click', function() {
        clearFilters();
    });
    
    // Analysis type change
    document.getElementById('analysisType').addEventListener('change', function() {
        updateFormForAnalysisType(this.value);
    });
}

function loadAvailableStations() {
    // This would typically load from an API endpoint
    // For now, we'll use some default stations
    const stationSelect = document.getElementById('stationFilter');
    const stations = ['WAVY001', 'WAVY002', 'WAVY003', 'WAVY004'];
    
    stations.forEach(station => {
        const option = document.createElement('option');
        option.value = station;
        option.textContent = station;
        stationSelect.appendChild(option);
    });
}

function executeAnalysis() {
    const formData = new FormData(document.getElementById('analysisForm'));
    const analysisRequest = {};
    
    // Convert FormData to object
    for (let [key, value] of formData.entries()) {
        if (value !== '') {
            // Convert numeric values
            if (['waveHeightMin', 'waveHeightMax', 'wavePeriodMin', 'wavePeriodMax',
                 'windSpeedMin', 'windSpeedMax', 'windDirectionMin', 'windDirectionMax',
                 'seaTemperatureMin', 'seaTemperatureMax', 'airTemperatureMin', 'airTemperatureMax',
                 'pressureMin', 'pressureMax', 'qcFlagMax'].includes(key)) {
                analysisRequest[key] = parseFloat(value);
            } else if (['startDate', 'endDate'].includes(key)) {
                analysisRequest[key] = new Date(value).toISOString();
            } else {
                analysisRequest[key] = value;
            }
        }
    }
    
    // Validate required fields
    if (!analysisRequest.startDate || !analysisRequest.endDate) {
        showErrorMessage('Por favor, selecione as datas de início e fim');
        return;
    }
    
    if (new Date(analysisRequest.startDate) >= new Date(analysisRequest.endDate)) {
        showErrorMessage('A data de início deve ser anterior à data de fim');
        return;
    }
    
    showLoadingIndicator();
    hideResults();
    
    fetch('/Home/AnalyzeDataWithFilters', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(analysisRequest)
    })
    .then(response => response.json())
    .then(result => {
        hideLoadingIndicator();
        
        if (result.success) {
            currentAnalysis = result;
            displayAnalysisResults(result);
            showSuccessMessage('Análise concluída com sucesso!');
        } else {
            showErrorMessage(result.error || 'Erro na análise dos dados');
        }
    })
    .catch(error => {
        hideLoadingIndicator();
        console.error('Error executing analysis:', error);
        showErrorMessage('Erro ao executar análise: ' + error.message);
    });
}

function displayAnalysisResults(result) {
    const resultsContainer = document.getElementById('resultsContent');
    const data = result.data;
    
    if (!data || data.message) {
        resultsContainer.innerHTML = `
            <div class="alert alert-warning">
                <i class="bi bi-exclamation-triangle me-2"></i>
                ${data?.message || 'Nenhum dado encontrado com os filtros aplicados'}
            </div>
        `;
        showResults();
        return;
    }
    
    let html = '';
    
    // Summary Section
    if (data.summary) {
        html += generateSummarySection(data.summary);
    }
    
    // Statistics Section
    if (data.statistics) {
        html += generateStatisticsSection(data.statistics);
    }
    
    // Trends Section
    if (data.trends) {
        html += generateTrendsSection(data.trends);
    }
    
    // Patterns Section
    if (data.patterns) {
        html += generatePatternsSection(data.patterns);
    }
    
    // Extreme Events Section
    if (data.extremeEvents && data.extremeEvents.length > 0) {
        html += generateExtremeEventsSection(data.extremeEvents);
    }
    
    resultsContainer.innerHTML = html;
    showResults();
}

function generateSummarySection(summary) {
    return `
        <div class="row mb-4">
            <div class="col-12">
                <h6 class="text-primary mb-3">
                    <i class="bi bi-info-circle me-2"></i>
                    Resumo da Análise
                </h6>
                <div class="row g-3">
                    <div class="col-md-3">
                        <div class="result-metric">
                            <div class="result-value">${summary.totalRecords.toLocaleString()}</div>
                            <div class="result-label">Registos Analisados</div>
                        </div>
                    </div>
                    <div class="col-md-3">
                        <div class="result-metric">
                            <div class="result-value">${summary.stations.length}</div>
                            <div class="result-label">Estações</div>
                        </div>
                    </div>
                    <div class="col-md-3">
                        <div class="result-metric">
                            <div class="result-value">${formatDuration(summary.timeSpan.start, summary.timeSpan.end)}</div>
                            <div class="result-label">Período</div>
                        </div>
                    </div>
                    <div class="col-md-3">
                        <div class="result-metric">
                            <div class="result-value">${summary.stations.join(', ')}</div>
                            <div class="result-label">Estações Ativas</div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;
}

function generateStatisticsSection(stats) {
    return `
        <div class="row mb-4">
            <div class="col-12">
                <h6 class="text-primary mb-3">
                    <i class="bi bi-bar-chart me-2"></i>
                    Estatísticas Detalhadas
                </h6>
                <div class="row g-4">
                    ${stats.waves ? generateWaveStats(stats.waves) : ''}
                    ${stats.wind ? generateWindStats(stats.wind) : ''}
                    ${stats.temperature ? generateTemperatureStats(stats.temperature) : ''}
                    ${stats.pressure ? generatePressureStats(stats.pressure) : ''}
                </div>
            </div>
        </div>
    `;
}

function generateWaveStats(waves) {
    return `
        <div class="col-md-6">
            <div class="card border-primary">
                <div class="card-header bg-primary text-white">
                    <h6 class="mb-0"><i class="bi bi-water me-2"></i>Estatísticas de Ondas</h6>
                </div>
                <div class="card-body">
                    <div class="row g-3">
                        <div class="col-6">
                            <small class="text-muted">Altura Média</small>
                            <div class="fw-bold text-primary">${waves.avgHeight.toFixed(2)} m</div>
                        </div>
                        <div class="col-6">
                            <small class="text-muted">Altura Máxima</small>
                            <div class="fw-bold text-danger">${waves.maxHeight.toFixed(2)} m</div>
                        </div>
                        <div class="col-6">
                            <small class="text-muted">Período Médio</small>
                            <div class="fw-bold text-info">${waves.avgPeriod.toFixed(1)} s</div>
                        </div>
                        <div class="col-6">
                            <small class="text-muted">Altura Significativa</small>
                            <div class="fw-bold text-warning">${waves.significantWaveHeight.toFixed(2)} m</div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;
}

function generateWindStats(wind) {
    return `
        <div class="col-md-6">
            <div class="card border-warning">
                <div class="card-header bg-warning text-white">
                    <h6 class="mb-0"><i class="bi bi-wind me-2"></i>Estatísticas de Vento</h6>
                </div>
                <div class="card-body">
                    <div class="row g-3">
                        <div class="col-6">
                            <small class="text-muted">Velocidade Média</small>
                            <div class="fw-bold text-warning">${wind.avgSpeed.toFixed(1)} m/s</div>
                        </div>
                        <div class="col-6">
                            <small class="text-muted">Velocidade Máxima</small>
                            <div class="fw-bold text-danger">${wind.maxSpeed.toFixed(1)} m/s</div>
                        </div>
                        <div class="col-6">
                            <small class="text-muted">Rajada Média</small>
                            <div class="fw-bold text-info">${wind.avgGust.toFixed(1)} m/s</div>
                        </div>
                        <div class="col-6">
                            <small class="text-muted">Direção Predominante</small>
                            <div class="fw-bold text-primary">${wind.predominantDirection}</div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;
}

function generateTemperatureStats(temp) {
    return `
        <div class="col-md-6">
            <div class="card border-success">
                <div class="card-header bg-success text-white">
                    <h6 class="mb-0"><i class="bi bi-thermometer me-2"></i>Estatísticas de Temperatura</h6>
                </div>
                <div class="card-body">
                    <div class="row g-3">
                        <div class="col-6">
                            <small class="text-muted">Temp. Mar Média</small>
                            <div class="fw-bold text-success">${temp.avgSea.toFixed(1)}°C</div>
                        </div>
                        <div class="col-6">
                            <small class="text-muted">Temp. Mar Máxima</small>
                            <div class="fw-bold text-danger">${temp.maxSea.toFixed(1)}°C</div>
                        </div>
                        <div class="col-6">
                            <small class="text-muted">Temp. Mar Mínima</small>
                            <div class="fw-bold text-info">${temp.minSea.toFixed(1)}°C</div>
                        </div>
                        <div class="col-6">
                            <small class="text-muted">Temp. Ar Média</small>
                            <div class="fw-bold text-warning">${temp.avgAir.toFixed(1)}°C</div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;
}

function generatePressureStats(pressure) {
    return `
        <div class="col-md-6">
            <div class="card border-info">
                <div class="card-header bg-info text-white">
                    <h6 class="mb-0"><i class="bi bi-speedometer2 me-2"></i>Estatísticas de Pressão</h6>
                </div>
                <div class="card-body">
                    <div class="row g-3">
                        <div class="col-4">
                            <small class="text-muted">Pressão Média</small>
                            <div class="fw-bold text-info">${pressure.avg.toFixed(1)} hPa</div>
                        </div>
                        <div class="col-4">
                            <small class="text-muted">Pressão Máxima</small>
                            <div class="fw-bold text-success">${pressure.max.toFixed(1)} hPa</div>
                        </div>
                        <div class="col-4">
                            <small class="text-muted">Pressão Mínima</small>
                            <div class="fw-bold text-danger">${pressure.min.toFixed(1)} hPa</div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;
}

function generateTrendsSection(trends) {
    return `
        <div class="row mb-4">
            <div class="col-12">
                <h6 class="text-primary mb-3">
                    <i class="bi bi-graph-up-arrow me-2"></i>
                    Análise de Tendências
                </h6>
                <div class="row g-3">
                    <div class="col-md-4">
                        <div class="card">
                            <div class="card-body text-center">
                                <i class="bi bi-water text-primary mb-2" style="font-size: 2rem;"></i>
                                <h6>Altura das Ondas</h6>
                                <div class="trend-indicator ${trends.waveHeight.trend}">
                                    <i class="bi bi-arrow-${trends.waveHeight.trend === 'increasing' ? 'up' : 'down'}"></i>
                                    ${trends.waveHeight.trend === 'increasing' ? 'Aumentando' : 'Diminuindo'}
                                </div>
                                <small class="text-muted">${trends.waveHeight.change > 0 ? '+' : ''}${trends.waveHeight.change.toFixed(2)} m</small>
                            </div>
                        </div>
                    </div>
                    <div class="col-md-4">
                        <div class="card">
                            <div class="card-body text-center">
                                <i class="bi bi-thermometer text-success mb-2" style="font-size: 2rem;"></i>
                                <h6>Temperatura do Mar</h6>
                                <div class="trend-indicator ${trends.temperature.trend}">
                                    <i class="bi bi-arrow-${trends.temperature.trend === 'increasing' ? 'up' : 'down'}"></i>
                                    ${trends.temperature.trend === 'increasing' ? 'Aumentando' : 'Diminuindo'}
                                </div>
                                <small class="text-muted">${trends.temperature.change > 0 ? '+' : ''}${trends.temperature.change.toFixed(1)}°C</small>
                            </div>
                        </div>
                    </div>
                    <div class="col-md-4">
                        <div class="card">
                            <div class="card-body text-center">
                                <i class="bi bi-wind text-warning mb-2" style="font-size: 2rem;"></i>
                                <h6>Velocidade do Vento</h6>
                                <div class="trend-indicator ${trends.windSpeed.trend}">
                                    <i class="bi bi-arrow-${trends.windSpeed.trend === 'increasing' ? 'up' : 'down'}"></i>
                                    ${trends.windSpeed.trend === 'increasing' ? 'Aumentando' : 'Diminuindo'}
                                </div>
                                <small class="text-muted">${trends.windSpeed.change > 0 ? '+' : ''}${trends.windSpeed.change.toFixed(1)} m/s</small>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;
}

function generatePatternsSection(patterns) {
    if (!patterns || patterns.length === 0) return '';
    
    return `
        <div class="row mb-4">
            <div class="col-12">
                <h6 class="text-primary mb-3">
                    <i class="bi bi-pattern me-2"></i>
                    Padrões Identificados
                </h6>
                <div class="row g-3">
                    ${patterns.map(pattern => `
                        <div class="col-md-6">
                            <div class="card border-${pattern.type === 'storm' ? 'danger' : 'success'}">
                                <div class="card-body">
                                    <div class="d-flex align-items-center mb-2">
                                        <i class="bi bi-${pattern.type === 'storm' ? 'cloud-lightning' : 'sun'} text-${pattern.type === 'storm' ? 'danger' : 'success'} me-2"></i>
                                        <h6 class="mb-0">${pattern.type === 'storm' ? 'Condições de Tempestade' : 'Condições Calmas'}</h6>
                                    </div>
                                    <p class="mb-1">Ocorrências: <strong>${pattern.count}</strong> registos (${pattern.percentage.toFixed(1)}%)</p>
                                    ${pattern.avgIntensity ? `
                                        <small class="text-muted">
                                            Intensidade média: ${pattern.avgIntensity.waveHeight.toFixed(1)}m ondas, ${pattern.avgIntensity.windSpeed.toFixed(1)}m/s vento
                                        </small>
                                    ` : ''}
                                </div>
                            </div>
                        </div>
                    `).join('')}
                </div>
            </div>
        </div>
    `;
}

function generateExtremeEventsSection(events) {
    return `
        <div class="row mb-4">
            <div class="col-12">
                <h6 class="text-primary mb-3">
                    <i class="bi bi-exclamation-triangle me-2"></i>
                    Eventos Extremos (Top 10)
                </h6>
                <div class="table-responsive">
                    <table class="table table-striped">
                        <thead>
                            <tr>
                                <th>Timestamp</th>
                                <th>Tipo</th>
                                <th>Valor</th>
                                <th>Estação</th>
                                <th>Severidade</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${events.map(event => `
                                <tr>
                                    <td>${new Date(event.timestamp).toLocaleString('pt-PT')}</td>
                                    <td>
                                        <span class="badge bg-${event.type === 'extreme_wave' ? 'primary' : 'warning'}">
                                            ${event.type === 'extreme_wave' ? 'Onda Extrema' : 'Vento Extremo'}
                                        </span>
                                    </td>
                                    <td>${event.value.toFixed(2)} ${event.type === 'extreme_wave' ? 'm' : 'm/s'}</td>
                                    <td>${event.station}</td>
                                    <td>
                                        <span class="badge bg-${event.severity === 'critical' ? 'danger' : 'warning'}">
                                            ${event.severity === 'critical' ? 'Crítico' : 'Alto'}
                                        </span>
                                    </td>
                                </tr>
                            `).join('')}
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    `;
}

function clearFilters() {
    document.getElementById('analysisForm').reset();
    setDefaultDates();
    document.querySelectorAll('.preset-card').forEach(card => {
        card.classList.remove('active');
    });
    hideResults();
    showSuccessMessage('Filtros limpos com sucesso!');
}

function updateFormForAnalysisType(analysisType) {
    // This could expand/collapse certain filter sections based on analysis type
    console.log('Analysis type changed to:', analysisType);
}

function showLoadingIndicator() {
    document.getElementById('loadingIndicator').style.display = 'block';
    document.querySelector('.card-body').classList.add('analysis-loading');
}

function hideLoadingIndicator() {
    document.getElementById('loadingIndicator').style.display = 'none';
    document.querySelector('.card-body').classList.remove('analysis-loading');
}

function showResults() {
    document.getElementById('analysisResults').style.display = 'block';
}

function hideResults() {
    document.getElementById('analysisResults').style.display = 'none';
}

function formatDuration(start, end) {
    const startDate = new Date(start);
    const endDate = new Date(end);
    const diffMs = endDate - startDate;
    const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));
    
    if (diffDays > 0) {
        return `${diffDays} dias`;
    } else {
        const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
        return `${diffHours} horas`;
    }
}

function showSuccessMessage(message) {
    showToast(message, 'success');
}

function showErrorMessage(message) {
    showToast(message, 'error');
}

function showToast(message, type) {
    // Create toast element
    const toast = document.createElement('div');
    toast.className = `alert alert-${type === 'success' ? 'success' : 'danger'} alert-dismissible fade show position-fixed`;
    toast.style.cssText = 'top: 20px; right: 20px; z-index: 1050; min-width: 300px;';
    toast.innerHTML = `
        <i class="bi bi-${type === 'success' ? 'check-circle' : 'exclamation-triangle'} me-2"></i>
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;
    
    document.body.appendChild(toast);
    
    // Auto remove after 5 seconds
    setTimeout(() => {
        if (toast.parentNode) {
            toast.parentNode.removeChild(toast);
        }
    }, 5000);
}
