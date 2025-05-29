// Ocean Dashboard - Simplified Charts Implementation (No Loading System)
console.log('🌊 Ocean Dashboard simple charts initializing...');

// Chart configurations with consistent color scheme
const chartConfigs = {
    waveHeightChart: {
        title: 'Altura das Ondas (m)',
        field: 'waveHeight',
        color: '#4e73df', // Primary blue
        unit: 'm',
        themeClass: 'primary',
        icon: 'bi-water'
    },
    windDirectionChart: {
        title: 'Direção do Vento (°)',
        field: 'windDirection',
        color: '#36b9cc', // Info color
        unit: '°',
        themeClass: 'info',
        icon: 'bi-compass'
    },
    temperatureChart: {
        title: 'Temperatura da Água (°C)',
        field: 'seaTemperature',
        color: '#1cc88a', // Success green
        unit: '°C',
        themeClass: 'success',
        icon: 'bi-thermometer-half'
    },
    windSpeedChart: {
        title: 'Velocidade do Vento (m/s)',
        field: 'windSpeed',
        color: '#f6c23e', // Warning yellow
        unit: 'm/s',
        themeClass: 'warning',
        icon: 'bi-wind'
    }
};

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    console.log('DOM loaded');
    
    if (typeof Plotly === 'undefined') {
        console.error('❌ Plotly.js not loaded!');
        return;
    }
    
    // Create all charts immediately and load data
    createAllCharts();
    loadAndDisplayData();
    
    // Set up refresh button
    const refreshBtn = document.getElementById('refreshData');
    if (refreshBtn) {
        refreshBtn.addEventListener('click', loadAndDisplayData);
    }
    
    // Initialize Bootstrap tooltips
    const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    const tooltipList = [...tooltipTriggerList].map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl));
    
    // Apply consistent colors to table headers
    const headers = document.querySelectorAll('th.sort-header');
    headers.forEach(header => {
        const dataSort = header.getAttribute('data-sort');
        if (dataSort === 'wave-height') {
            header.classList.add('text-primary');
        } else if (dataSort === 'wind-speed') {
            header.classList.add('text-warning');
        } else if (dataSort === 'wind-direction') {
            header.classList.add('text-info');
        } else if (dataSort === 'temperature') {
            header.classList.add('text-success');
        }
    });
});

// Create all charts at once
function createAllCharts() {
    Object.keys(chartConfigs).forEach(chartId => {
        const element = document.getElementById(chartId);
        if (!element) {
            console.warn(`Chart element not found: ${chartId}`);
            return;
        }
        
        createChart(chartId);
    });
}

// Create a single chart with stylish configuration
function createChart(chartId) {
    const config = chartConfigs[chartId];
    const element = document.getElementById(chartId);
    
    if (!config || !element) {
        return;
    }
    
    // Apply themed border to the chart container
    const container = element.closest('.chart-card');
    if (container) {
        container.classList.add(`border-left-${config.themeClass}`);
        
        // Update chart header with consistent styling
        const header = container.querySelector('.card-header');
        if (header) {
            const titleElement = header.querySelector('.card-title');
            if (titleElement) {
                // Add icon if not already present
                if (!titleElement.innerHTML.includes('bi-')) {
                    titleElement.innerHTML = `<i class="bi ${config.icon} me-2"></i>${config.title}`;
                }
                
                // Ensure title has the right color class
                if (!titleElement.classList.contains(`text-${config.themeClass}`)) {
                    titleElement.className = `card-title mb-0 text-${config.themeClass}`;
                }
            }
        }
    }
    
    // Create empty chart with enhanced styling
    const trace = {
        x: [],
        y: [],
        type: 'scatter',
        mode: 'lines+markers',
        name: config.title,
        line: { 
            color: config.color, 
            width: 3,
            shape: 'spline' // Smooth curves
        },
        marker: { 
            color: config.color, 
            size: 8,
            symbol: 'circle',
            line: {
                color: '#ffffff',
                width: 1.5
            }
        },
        fill: 'tozeroy',
        fillcolor: `${config.color}15` // Very light fill color (15% opacity)
    };
    
    const layout = {
        title: {
            text: config.title,
            font: { 
                size: 16,
                color: '#5a5c69',
                family: "'Nunito', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif"
            }
        },
        xaxis: { 
            title: 'Tempo',
            showgrid: true,
            gridcolor: '#eaecf4',
            zeroline: false
        },
        yaxis: { 
            title: `${config.title}`,
            showgrid: true,
            gridcolor: '#eaecf4',
            zeroline: false
        },
        margin: { l: 60, r: 30, t: 60, b: 60 },
        font: { family: "'Nunito', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Arial, sans-serif" },
        plot_bgcolor: 'white',
        paper_bgcolor: 'white'
    };
    
    const plotConfig = {
        responsive: true,
        displayModeBar: true,
        displaylogo: false,
        modeBarButtonsToRemove: ['pan2d', 'lasso2d', 'select2d']
    };
    
    Plotly.newPlot(chartId, [trace], layout, plotConfig);
}

// Load data and update all charts
function loadAndDisplayData() {
    const timeRange = document.getElementById('timeRange')?.value || '24h';
    const location = document.getElementById('location')?.value || 'all';
    const url = `/Home/GetLatestData?timeRange=${timeRange}&location=${encodeURIComponent(location)}`;
    
    fetch(url)
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error: ${response.status}`);
            }
            return response.json();
        })
        .then(data => {
            if (data && data.length > 0) {
                updateAllCharts(data);
                updateDataTable(data);
                updateLastRefreshTime();
            }
        })
        .catch(error => {
            console.error('Error loading data:', error);
        });
}

// Update all charts with data
function updateAllCharts(data) {
    if (!data || data.length === 0) return;
    
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
        
        if (values.length > 0) {
            updateChart(chartId, timestamps.slice(0, values.length), values);
        }
    });
}

// Update single chart with data
function updateChart(chartId, timestamps, values) {
    const update = {
        x: [timestamps],
        y: [values]
    };
    
    Plotly.restyle(chartId, update, 0);
}

// Update data table with color-coded values
function updateDataTable(data) {
    const tableBody = document.querySelector('table tbody');
    if (!tableBody) return;
    
    if (!data || data.length === 0) {
        tableBody.innerHTML = '<tr><td colspan="8" class="text-center">Nenhum dado disponível</td></tr>';
        return;
    }
    
    tableBody.innerHTML = data.slice(0, 50).map(item => {
        // Apply conditional styling based on values
        const waveHeightClass = item.waveHeight > 3 ? 'text-danger' : item.waveHeight > 1.5 ? 'text-warning' : 'text-primary';
        const windSpeedClass = item.windSpeed > 25 ? 'text-danger' : item.windSpeed > 15 ? 'text-warning' : 'text-warning';
        const tempClass = item.seaTemperature > 25 ? 'text-danger' : item.seaTemperature < 15 ? 'text-info' : 'text-success';
        
        return `
        <tr>
            <td>
                <div class="d-flex flex-column">
                    <span class="fw-semibold">${new Date(item.timestamp).toLocaleDateString('pt-PT')}</span>
                    <small class="text-muted">${new Date(item.timestamp).toLocaleTimeString('pt-PT')}</small>
                </div>
            </td>
            <td><span class="badge bg-primary">${item.stationId || 'N/A'}</span></td>
            <td><span class="badge bg-secondary">${item.sensorId || 'N/A'}</span></td>
            <td class="text-end">
                <span class="fw-semibold ${waveHeightClass}">
                    <i class="bi bi-water me-1 small"></i>
                    ${item.waveHeight ? item.waveHeight.toFixed(2) + ' m' : 'N/A'}
                </span>
            </td>
            <td class="text-end">
                <span class="fw-semibold text-primary">
                    ${item.wavePeriod ? item.wavePeriod.toFixed(2) + ' s' : 'N/A'}
                </span>
            </td>
            <td class="text-end">
                <span class="fw-semibold ${windSpeedClass}">
                    <i class="bi bi-wind me-1 small"></i>
                    ${item.windSpeed ? item.windSpeed.toFixed(2) + ' m/s' : 'N/A'}
                </span>
            </td>
            <td class="text-end">
                <span class="fw-semibold text-info">
                    <i class="bi bi-compass me-1 small"></i>
                    ${item.windDirection ? item.windDirection.toFixed(1) + '°' : 'N/A'}
                </span>
            </td>
            <td class="text-end">
                <span class="fw-semibold ${tempClass}">
                    <i class="bi bi-thermometer-half me-1 small"></i>
                    ${item.seaTemperature ? item.seaTemperature.toFixed(1) + '°C' : 'N/A'}
                </span>
            </td>
        </tr>
    `;
    }).join('');
}

// Update last refresh time display
function updateLastRefreshTime() {
    const element = document.getElementById('lastUpdateTime');
    if (element) {
        element.textContent = new Date().toLocaleTimeString('pt-PT');
    }
}

// Global functions for external access
window.loadDashboardData = loadAndDisplayData;
