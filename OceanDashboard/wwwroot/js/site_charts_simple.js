// Ocean Dashboard - Simplified Charts Implementation (No Loading System)
console.log('🌊 Ocean Dashboard simple charts initializing...');

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

// Create a single chart
function createChart(chartId) {
    const config = chartConfigs[chartId];
    const element = document.getElementById(chartId);
    
    if (!config || !element) {
        return;
    }
    
    // Create empty chart with basic configuration
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

// Update data table
function updateDataTable(data) {
    const tableBody = document.querySelector('table tbody');
    if (!tableBody) return;
    
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
