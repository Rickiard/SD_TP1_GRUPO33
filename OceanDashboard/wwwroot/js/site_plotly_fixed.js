// Ocean Dashboard - Plotly.js Implementation (Fixed Loading Issue)
console.log('🌊 Loading Ocean Dashboard with Plotly.js (Fixed Version)...');

// Global variables
let dashboardData = [];
let charts = {};
let autoRefreshInterval = null;
let isInitializing = false;

// Chart configurations
const chartConfigs = {
    waveHeightChart: {
        title: 'Altura das Ondas (m)',
        field: 'waveHeight',
        color: '#007bff',
        unit: 'm'
    },
    windDirectionChart: {
        title: 'Direção do Vento (°)',
        field: 'windDirection',
        color: '#17a2b8',
        unit: '°'
    },
    temperatureChart: {
        title: 'Temperatura da Água (°C)',
        field: 'seaTemperature',
        color: '#28a745',
        unit: '°C'
    },
    windSpeedChart: {
        title: 'Velocidade do Vento (m/s)',
        field: 'windSpeed',
        color: '#ffc107',
        unit: 'm/s'
    }
};

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    console.log('🚀 DOM loaded, checking Plotly...');
    
    if (typeof Plotly === 'undefined') {
        console.error('❌ Plotly.js not loaded!');
        return;
    }
    
    console.log('✅ Plotly.js loaded successfully');
    isInitializing = true;
      // Initialize with proper sequencing
    initializeCharts()
        .then(() => {
            console.log('✅ Charts initialized, loading data...');
            return loadDashboardData();
        })
        .then(() => {
            console.log('✅ Data loaded, auto-refresh disabled');
            // startAutoRefresh(); // Auto-refresh removido
            isInitializing = false;
        })
        .catch(error => {
            console.error('❌ Initialization error:', error);
            isInitializing = false;
        });
});

async function initializeCharts() {
    console.log('🔧 Initializing charts...');
    
    const promises = Object.keys(chartConfigs).map(chartId => {
        return new Promise((resolve, reject) => {
            const element = document.getElementById(chartId);
            if (!element) {
                console.warn(`⚠️ Chart element not found: ${chartId}`);
                resolve();
                return;
            }
            
            console.log(`📊 Creating chart: ${chartId}`);
            createChart(chartId)
                .then(() => resolve())
                .catch(error => {
                    console.error(`❌ Error creating ${chartId}:`, error);
                    resolve(); // Continue with other charts
                });
        });
    });
    
    await Promise.all(promises);
    console.log('✅ All charts initialized');
}

function createChart(chartId) {
    return new Promise((resolve, reject) => {
        const config = chartConfigs[chartId];
        const element = document.getElementById(chartId);
        
        if (!config || !element) {
            console.error(`❌ Missing config or element for: ${chartId}`);
            reject(new Error(`Missing config or element for: ${chartId}`));
            return;
        }
        
        // Show loading state
        element.innerHTML = `
            <div class="d-flex justify-content-center align-items-center" style="height: 300px;">
                <div class="text-center">
                    <div class="spinner-border text-primary mb-2" role="status"></div>
                    <div>Carregando ${config.title}...</div>
                </div>
            </div>
        `;
        
        // Create empty chart
        const trace = {
            x: [],
            y: [],
            type: 'scatter',
            mode: 'lines+markers',
            name: config.title,
            line: { color: config.color, width: 2 },
            marker: { color: config.color, size: 6 }
        };
        
        const layout = {
            title: {
                text: config.title,
                font: { size: 16 }
            },
            xaxis: { 
                title: 'Tempo',
                showgrid: true
            },
            yaxis: { 
                title: `${config.title}`,
                showgrid: true
            },
            margin: { l: 60, r: 30, t: 60, b: 60 },
            font: { family: 'Arial, sans-serif' },
            plot_bgcolor: '#fafafa',
            paper_bgcolor: 'white'
        };
        
        const plotConfig = {
            responsive: true,
            displayModeBar: true,
            displaylogo: false,
            modeBarButtonsToRemove: ['pan2d', 'lasso2d', 'select2d']
        };
        
        Plotly.newPlot(chartId, [trace], layout, plotConfig)
            .then(() => {
                charts[chartId] = true;
                console.log(`✅ Chart created successfully: ${chartId}`);
                resolve();
            })
            .catch(error => {
                console.error(`❌ Error creating chart ${chartId}:`, error);
                element.innerHTML = `
                    <div class="d-flex justify-content-center align-items-center" style="height: 300px;">
                        <div class="text-center text-danger">
                            <i class="bi bi-exclamation-triangle mb-2" style="font-size: 2rem;"></i>
                            <div>Erro ao carregar gráfico</div>
                            <small>${error.message}</small>
                        </div>
                    </div>
                `;
                reject(error);
            });
    });
}

function loadDashboardData() {
    return new Promise((resolve, reject) => {
        console.log('📡 Loading dashboard data...');
        
        const timeRange = document.getElementById('timeRange')?.value || '24h';
        const location = document.getElementById('location')?.value || 'all';
        const url = `/Home/GetLatestData?timeRange=${timeRange}&location=${encodeURIComponent(location)}`;
        
        fetch(url)
            .then(response => {
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }
                return response.json();
            })
            .then(data => {
                console.log(`📊 Received ${data.length} records`);
                dashboardData = data;
                
                if (data.length === 0) {
                    showNoDataMessage();
                    resolve();
                    return;
                }
                
                updateAllCharts(data);
                updateDataTable(data);
                updateLastRefreshTime();
                resolve();
            })
            .catch(error => {
                console.error('❌ Error loading data:', error);
                showError(`Erro ao carregar dados: ${error.message}`);
                reject(error);
            });
    });
}

function updateAllCharts(data) {
    if (!data || data.length === 0) {
        console.warn('⚠️ No data to update charts');
        return;
    }
    
    console.log('📈 Updating all charts with data...');
    
    // Prepare timestamps
    const timestamps = data.map(item => {
        const date = new Date(item.timestamp);
        return date.toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' });
    });
    
    // Update each chart
    Object.keys(chartConfigs).forEach(chartId => {
        const config = chartConfigs[chartId];
        const values = data.map(item => {
            const value = item[config.field];
            return value !== null && value !== undefined ? parseFloat(value) : 0;
        }).filter(v => !isNaN(v));
        
        if (values.length === 0) {
            console.warn(`⚠️ No valid data for chart: ${chartId}`);
            return;
        }
        
        // Only update if chart is properly initialized
        if (charts[chartId]) {
            updateChart(chartId, timestamps.slice(0, values.length), values);
        } else {
            console.warn(`⚠️ Chart ${chartId} not ready for updates`);
        }
    });
}

function updateChart(chartId, timestamps, values) {
    if (!charts[chartId]) {
        console.warn(`⚠️ Chart ${chartId} not initialized, skipping update`);
        return;
    }
    
    const update = {
        x: [timestamps],
        y: [values]
    };
    
    Plotly.restyle(chartId, update, 0)
        .then(() => {
            console.log(`✅ Chart updated: ${chartId} (${values.length} points)`);
        })
        .catch(error => {
            console.error(`❌ Error updating chart ${chartId}:`, error);
        });
}

function updateDataTable(data) {
    console.log('📋 Updating data table...');
    
    const tableBody = document.querySelector('table tbody');
    if (!tableBody) {
        console.warn('⚠️ Table body not found');
        return;
    }
    
    if (!data || data.length === 0) {
        tableBody.innerHTML = '<tr><td colspan="8" class="text-center">Nenhum dado disponível</td></tr>';
        return;
    }
    
    tableBody.innerHTML = data.slice(0, 50).map(item => `
        <tr>
            <td>${new Date(item.timestamp).toLocaleString('pt-PT')}</td>
            <td><span class="badge bg-primary">${item.stationId || 'N/A'}</span></td>
            <td><span class="badge bg-secondary">${item.sensorId || 'N/A'}</span></td>
            <td class="text-end">${item.waveHeight ? item.waveHeight.toFixed(2) + ' m' : 'N/A'}</td>
            <td class="text-end">${item.wavePeriod ? item.wavePeriod.toFixed(2) + ' s' : 'N/A'}</td>
            <td class="text-end">${item.windSpeed ? item.windSpeed.toFixed(2) + ' m/s' : 'N/A'}</td>
            <td class="text-end">${item.windDirection ? item.windDirection.toFixed(1) + '°' : 'N/A'}</td>
            <td class="text-end">${item.seaTemperature ? item.seaTemperature.toFixed(1) + '°C' : 'N/A'}</td>
        </tr>
    `).join('');
    
    console.log(`📊 Table updated with ${data.length} records`);
}

function startAutoRefresh() {
    if (autoRefreshInterval) {
        clearInterval(autoRefreshInterval);
    }
    
    autoRefreshInterval = setInterval(() => {
        if (!isInitializing) {
            console.log('🔄 Auto-refresh triggered');
            loadDashboardData();
        }
    }, 30000); // 30 seconds
    
    console.log('🔄 Auto-refresh started (30s interval)');
}

function showError(message) {
    console.error('💥 Error:', message);
    // Show error in charts
    Object.keys(chartConfigs).forEach(chartId => {
        const element = document.getElementById(chartId);
        if (element && !charts[chartId]) {
            element.innerHTML = `
                <div class="d-flex justify-content-center align-items-center" style="height: 300px;">
                    <div class="text-center text-danger">
                        <i class="bi bi-exclamation-triangle mb-2" style="font-size: 2rem;"></i>
                        <div>Erro</div>
                        <small>${message}</small>
                    </div>
                </div>
            `;
        }
    });
}

function showNoDataMessage() {
    console.log('📭 No data available');
    Object.keys(chartConfigs).forEach(chartId => {
        const element = document.getElementById(chartId);
        if (element) {
            element.innerHTML = `
                <div class="d-flex justify-content-center align-items-center" style="height: 300px;">
                    <div class="text-center text-muted">
                        <i class="bi bi-graph-up mb-2" style="font-size: 2rem;"></i>
                        <div>Nenhum dado disponível</div>
                    </div>
                </div>
            `;
        }
    });
}

function updateLastRefreshTime() {
    const element = document.getElementById('lastUpdateTime');
    if (element) {
        element.textContent = new Date().toLocaleTimeString('pt-PT');
    }
}

// Global functions for external access
window.loadDashboardData = loadDashboardData;
window.createChart = createChart;
window.startAutoRefresh = startAutoRefresh;

console.log('✅ Ocean Dashboard script loaded (Fixed Version)');
