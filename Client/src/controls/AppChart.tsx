import { BindableProperty } from "@web-atoms/core/dist/core/BindableProperty";
import { AtomControl } from "@web-atoms/core/dist/web/controls/AtomControl";
import * as ImportedChart from "chart.js/dist/chart.min.js";
import type { Chart } from "chart.js/types/index.esm";

export const CHART_COLORS = {
    red: "rgb(255, 99, 132)",
    orange: "rgb(255, 159, 64)",
    yellow: "rgb(255, 205, 86)",
    green: "rgb(75, 192, 192)",
    blue: "rgb(54, 162, 235)",
    purple: "rgb(153, 102, 255)",
    grey: "rgb(201, 203, 207)"
  };

export const CHART_BG = {
    red: "rgb(255, 99, 132, 0.5)",
    orange: "rgb(255, 159, 64, 0.5)",
    yellow: "rgb(255, 205, 86, 0.5)",
    green: "rgb(75, 192, 192, 0.5)",
    blue: "rgb(54, 162, 235, 0.5)",
    purple: "rgb(153, 102, 255, 0.5)",
    grey: "rgb(201, 203, 207, 0.5)"
  };
export default class AppChart extends AtomControl {

    public chart: Chart = null;

	@BindableProperty
    public chartData: any;

    private firstTime: boolean = true;

    private canvas: HTMLCanvasElement;

    public onPropertyChanged(name: string): void {
        super.onPropertyChanged(name);
        if (name === "chartData") {
            this.createChart();
        }
    }

    public dispose(): void {
        if (this.chart) {
            this.chart.canvas.remove();
            this.chart.destroy();
            this.chart = null;
        }
        super.dispose();
    }

    public createChart(delay: boolean = false): void {

        if (this.chart) {
            this.chart.canvas.remove();
            this.chart.destroy();
            this.chart = null;
        }

        if (!this.chartData) {
            return;
        }
        setTimeout(() => {
            this.chart = new ImportedChart(this.canvas, this.chartData);
        }, 100);
    }

    protected preCreate(): void {
        this.chartData = null;
        this.element.style.position = "relative";
        this.canvas = document.createElement("canvas");
        this.element.appendChild(this.canvas);
    }
}
