// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Função para atualizar os gráficos com novos dados
function updateCharts() {
    fetch('/Home/GetLatestData')
        .then(response => response.json())
        .then(data => {
            // Atualizar dados da tabela
            const tableBody = document.querySelector('table tbody');
            tableBody.innerHTML = data.slice(0, 10).map(item => `
                <tr>
                    <td>${new Date(item.timestamp).toLocaleString()}</td>
                    <td>${item.location}</td>
                    <td>${item.waveHeight.toFixed(2)}</td>
                    <td>${item.wavePeriod.toFixed(2)}</td>
                    <td>${item.waveDirection.toFixed(2)}</td>
                    <td>${item.temperature.toFixed(2)}</td>
                </tr>
            `).join('');

            // Atualizar gráficos
            const timestamps = data.map(d => new Date(d.timestamp).toLocaleString());
            const waveHeights = data.map(d => d.waveHeight);
            const waveDirections = data.map(d => d.waveDirection);
            const temperatures = data.map(d => d.temperature);
            const wavePeriods = data.map(d => d.wavePeriod);

            window.charts.forEach(chart => {
                switch(chart.canvas.id) {
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
                chart.update();
            });
        })
        .catch(error => console.error('Erro ao atualizar dados:', error));
}

// Armazenar referências aos gráficos
window.charts = [];

// Adicionar gráfico à lista quando criado
function addChart(chart) {
    if (!window.charts) {
        window.charts = [];
    }
    window.charts.push(chart);
}

// Atualizar dados a cada 30 segundos
setInterval(updateCharts, 30000);
