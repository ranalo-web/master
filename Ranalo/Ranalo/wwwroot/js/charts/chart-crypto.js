!(function (NioApp, $) {
    "use strict";
    //////// for developer - accountBalance //////// 
    // Avilable options to pass from outside 
    // labels: array,
    // dataUnit: string, (Used in tooltip or other section for display) 
    // datasets: [{label : string, color: string (color code with # or other format), data: array}]

    var mainBalance = {
        labels : ["01 Nov", "02 Nov", "03 Nov", "04 Nov", "05 Nov", "06 Nov", "07 Nov", "08 Nov", "09 Nov", "10 Nov", "11 Nov", "12 Nov", "13 Nov", "14 Nov", "15 Nov", "16 Nov", "17 Nov", "18 Nov", "19 Nov", "20 Nov", "21 Nov", "22 Nov", "23 Nov", "24 Nov", "25 Nov", "26 Nov", "27 Nov", "28 Nov", "29 Nov", "30 Nov"],
        dataUnit : 'BTC',
        datasets : [{
            label : "Send",
            color : "#6baafe",
            data: [110, 80, 125, 55, 95, 75, 90, 110, 80, 125, 55, 95, 75, 90, 110, 80, 125, 55, 95, 75, 90, 110, 80, 125, 55, 95, 75, 90, 75, 90]
        },{
            label : "Receive",
            color : "#baaeff",
            data: [80, 54, 105, 120, 82, 85, 60, 80, 54, 105, 120, 82, 85, 60, 80, 54, 105, 120, 82, 85, 60, 80, 54, 105, 120, 82, 85, 60, 85, 60]
        },{
            label : "Withdraw",
            color : "#a7ccff",
            data: [90, 98, 115, 70, 87, 95, 67, 90, 98, 115, 70, 87, 95, 67, 90, 98, 115, 70, 87, 95, 67, 90, 98, 115, 70, 87, 95, 67, 95, 67]
        }]
    };

    function accountBalance(selector, set_data){
        var $selector = (selector) ? $(selector) : $('.chart-account-balance');
        $selector.each(function(){
            var $self = $(this), _self_id = $self.attr('id'), _get_data = (typeof set_data === 'undefined') ? eval(_self_id) : set_data;

            var selectCanvas = document.getElementById(_self_id).getContext("2d");
            var chart_data = [];
            for (var i = 0; i < _get_data.datasets.length; i++) {
                chart_data.push({
                    label: _get_data.datasets[i].label,
                    data: _get_data.datasets[i].data,
                    // Styles
                    backgroundColor: _get_data.datasets[i].color,
                    borderWidth:2,
                    borderColor: 'transparent',
                    hoverBorderColor : 'transparent',
                    borderSkipped : 'bottom',
                    barPercentage : NioApp.State.asMobile ? 1 : .75,
                    categoryPercentage : NioApp.State.asMobile ? 1 : .75
                });
            } 
            var chart = new Chart(selectCanvas, {
                type: 'bar',
                data: {
                    labels: _get_data.labels,
                    datasets: chart_data,
                },
                options: {
                    plugins: {
                        legend: {
                            display: false,
                        },
                        tooltip: {
                            enabled: true,
                            rtl: NioApp.State.isRTL,
                            callbacks: {
                                label: function (context) {
                                    return `${context.parsed.y} ${_get_data.dataUnit}`;
                                },
                            },
                            backgroundColor: '#eff6ff',
                            titleFont:{
                                size: 13,
                            },
                            titleColor: '#6783b8',
                            titleMarginBottom: 6,
                            bodyColor: '#9eaecf',
                            bodyFont:{
                                size: 12
                            },
                            bodySpacing:4,
                            padding: 10,
                            footerMarginTop: 0,
                            displayColors: false
                        },
                    },
                    maintainAspectRatio: false,
                    scales: {
                        y: {
                            display: false,
                        },
                        x: {
                            display: false,
                            reverse: NioApp.State.isRTL
                        }
                    }
                }
            });
        })
    }
    // init accountBalance
    NioApp.coms.docReady.push(function(){ accountBalance(); });

    //////// for developer - referStats //////// 
    // Avilable options to pass from outside 
    // labels: array,
    // dataUnit: string, (Used in tooltip or other section for display) 
    // datasets: [{label : string, color: string (color code with # or other format), data: array}]

    var refBarChart = {
        labels : ["01 Nov", "02 Nov", "03 Nov", "04 Nov", "05 Nov", "06 Nov", "07 Nov", "08 Nov", "09 Nov", "10 Nov", "11 Nov", "12 Nov", "13 Nov", "14 Nov", "15 Nov", "16 Nov", "17 Nov", "18 Nov", "19 Nov", "20 Nov", "21 Nov", "22 Nov", "23 Nov", "24 Nov", "25 Nov", "26 Nov", "27 Nov", "28 Nov", "29 Nov", "30 Nov"],
        dataUnit : 'People',
        datasets : [{
            label : "Join",
            color : "#6baafe",
            data: [110, 80, 125, 55, 95, 75, 90, 110, 80, 125, 55, 95, 75, 90, 110, 80, 125, 55, 95, 75, 90, 110, 80, 125, 55, 95, 75, 90, 75, 90]
        }]
    };

    function referStats(selector, set_data){
        var $selector = (selector) ? $(selector) : $('.chart-refer-stats');
        $selector.each(function(){
            var $self = $(this), _self_id = $self.attr('id'), _get_data = (typeof set_data === 'undefined') ? eval(_self_id) : set_data;

            var selectCanvas = document.getElementById(_self_id).getContext("2d");
            var chart_data = [];
            for (var i = 0; i < _get_data.datasets.length; i++) {
                chart_data.push({
                    label: _get_data.datasets[i].label,
                    data: _get_data.datasets[i].data,
                    // Styles
                    backgroundColor: _get_data.datasets[i].color,
                    borderWidth:2,
                    borderColor: 'transparent',
                    hoverBorderColor : 'transparent',
                    borderSkipped : 'bottom',
                    barPercentage : .8,
                    categoryPercentage : .8
                });
            } 
            var chart = new Chart(selectCanvas, {
                type: 'bar',
                data: {
                    labels: _get_data.labels,
                    datasets: chart_data,
                },
                options: {
                    plugins: {
                        legend: {
                            display: false,
                        },
                        tooltip: {
                            enabled: true,
                            rtl: NioApp.State.isRTL,
                            callbacks: {
                                label: function (context) {
                                    return `${context.parsed.y} ${_get_data.dataUnit}`;
                                },
                            },
                            backgroundColor: '#fff',
                            titleFont:{
                                size: 13,
                            },
                            titleColor: '#6783b8',
                            titleMarginBottom: 6,
                            bodyColor: '#9eaecf',
                            bodyFont:{
                                size: 12
                            },
                            bodySpacing:4,
                            padding: 10,
                            footerMarginTop: 0,
                            displayColors: false
                        },
                    },
                    maintainAspectRatio: false,
                    scales: {
                        y: {
                            display: false,
                            ticks: {
                                beginAtZero: true
                            },
                        },
                        x: {
                            display: false,
                            reverse: NioApp.State.isRTL
                        }
                    }
                }
            });
        })
    }
    // init chart
    NioApp.coms.docReady.push(function(){ referStats(); });


    //////// for developer - accountSummary //////// 
    // Avilable options to pass from outside 
    // labels: array,
    // dataUnit: string, (Used in tooltip or other section for display) 
    // datasets: [{label : string, color: string (color code with # or other format), data: array}]
    var summaryBalance = {
        labels : ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"],
        dataUnit : 'BTC',
        datasets : [{
            label : "Total Received",
            color : "#5ce0aa",
            data: [110, 80, 125, 55, 95, 75, 90, 110, 80, 125, 55, 95]
        },{
            label : "Total Send",
            color : "#3a8dfe",
            data: [80, 54, 105, 120, 82, 85, 60, 80, 54, 105, 120, 82]
        },{
            label : "Total Withdraw",
            color : "#f6ca3e",
            data: [90, 98, 115, 70, 87, 95, 67, 90, 98, 115, 70, 87]
        }]
    };

    function accountSummary(selector, set_data){
        var $selector = (selector) ? $(selector) : $('.chart-account-summary');
        $selector.each(function(){
            var $self = $(this), _self_id = $self.attr('id'), _get_data = (typeof set_data === 'undefined') ? eval(_self_id) : set_data;
            var selectCanvas = document.getElementById(_self_id).getContext("2d");

            var chart_data = [];
            for (var i = 0; i < _get_data.datasets.length; i++) {
                chart_data.push({
                    label: _get_data.datasets[i].label,
                    tension:.4,
                    backgroundColor: 'transparent',
                    fill: true,
                    borderWidth:2,
                    borderColor: _get_data.datasets[i].color,
                    pointBorderColor: 'transparent',
                    pointBackgroundColor: 'transparent',
                    pointHoverBackgroundColor: "#fff",
                    pointHoverBorderColor: _get_data.datasets[i].color,
                    pointBorderWidth: 2,
                    pointHoverRadius: 4,
                    pointHoverBorderWidth: 2,
                    pointRadius: 4,
                    pointHitRadius: 4,
                    data: _get_data.datasets[i].data,
                });
            } 
            var chart = new Chart(selectCanvas, {
                type: 'line',
                data: {
                    labels: _get_data.labels,
                    datasets: chart_data,
                },
                options: {
                    plugins: {
                        legend: {
                            display: false,
                        },
                        tooltip: {
                            rtl: NioApp.State.isRTL,
                            callbacks: {
                                label: function (context) {
                                    return `${context.parsed.y} ${_get_data.dataUnit}`;
                                },
                            },
                            backgroundColor: '#eff6ff',
                            titleFont:{
                                size: 13,
                            },
                            titleColor: '#6783b8',
                            titleMarginBottom: 6,
                            bodyColor: '#9eaecf',
                            bodyFont:{
                                size: 12
                            },
                            bodySpacing:4,
                            padding: 10,
                            footerMarginTop: 0,
                            displayColors: false
                        },
                    },
                    maintainAspectRatio: false,
                    scales: {
                        y: {
                            position : NioApp.State.isRTL ? "right" : "left",
                            ticks: {
                                beginAtZero: false,
                                font: {
                                    size: 12,
                                },
                                color:'#9eaecf',
                                padding: 10
                            },
                            grid: { 
                                color: NioApp.hexRGB("#526484",.2),
                                tickLength:0,
                                zeroLineColor: NioApp.hexRGB("#526484",.2),
                                drawTicks:false,
                            },
                        },
                        x: {
                            ticks: {
                                font: {
                                    size: 12,
                                },
                                color:'#9eaecf',
                                source: 'auto',
                                padding: 5,
                            },
                            reverse: NioApp.State.isRTL,
                            grid: {
                                color: "transparent",
                                tickLength:20,
                                zeroLineColor: NioApp.hexRGB("#526484",.2),
                                offset: true,
                                drawTicks:false,
                            }
                        }
                    }
                }
            });
        })
    }

    // init accountSummary
    NioApp.coms.docReady.push(function(){ accountSummary(); });

})(NioApp, jQuery);