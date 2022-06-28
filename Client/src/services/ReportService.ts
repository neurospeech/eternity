import Colors from "@web-atoms/core/dist/core/Colors";
import DISingleton from "@web-atoms/core/dist/di/DISingleton";
import { Inject } from "@web-atoms/core/dist/di/Inject";
import DateTime from "@web-atoms/date-time/dist/DateTime";
import EF from "@web-atoms/entity/dist/EF";
import IPagedList from "@web-atoms/entity/dist/models/IPagedList";
import Query from "@web-atoms/entity/dist/services/Query";
import CacheTTL from "../common/CacheTTL";
import { CHART_BG, CHART_COLORS } from "../controls/AppChart";
import DateService from "./DateService";

const cacheSeconds = CacheTTL.OneMinute;

const primary = {
    borderColor: CHART_COLORS.purple,
    backgroundColor: CHART_BG.purple
};

const secondary = {
    borderColor: CHART_COLORS.grey,
    backgroundColor: CHART_BG.grey
};

interface IGraphQuery {
    x: any;
    [k: string]: any;
}

async function verticalBarGraphQuery<T extends IGraphQuery>(
    query: Promise<IPagedList<IGraphQuery>>,
    options: { [n in keyof Omit<T, "x">]: {
        label: string,
        borderColor: string,
        backgroundColor: string
    } },
    title?: string,
    label: (item) => string = (item) => item instanceof Date ? DateService.monthDay(item) : item) {
    const result = (await query).items;
    let datasets = null;
    const labels: string[] = [];
    for (const all of result) {
        if (!datasets) {
            datasets = [];
        }
        let index = 0;
        for (const key in options) {
            if (key === "x") {
                continue;
            }
            if (Object.prototype.hasOwnProperty.call(options, key)) {
                const element = all[key] || undefined;
                const { data } = datasets[index++] ??= { ... options[key], data: [], axis: "y" };
                data.push(element);
            }
        }
        labels.push(label(all.x));
    }
    return {
        type: "bar",
        data: {
            labels,
            datasets
        },
        options: {
            indexAxis: "y",
            fullSize: false,
            interaction: {
                mode: "index",
                intersect: false
            },
            plugins: {
                title: {
                    display: !!title,
                    text: title
                }
            },
            scales: {
                x: {
                    ticks: {
                        callback(val, index) {
                            // console.log(this);
                            return (index === 0 || index === this.max)
                                ? this.getLabelForValue(val)
                                : "";
                        }
                    },
                    grid: {
                        display: false
                    }
                },
                y: {
                    grid: {
                        display: false
                    }
                }

            }
        }
    };
}

async function lineGraphQuery<T extends IGraphQuery>(
    query: Query<IGraphQuery>,
    options: { [n in keyof Omit<T, "x">]: {
        label: string,
        borderColor: string,
        backgroundColor: string
    } },
    title?: string,
    label: (item) => string = (item) => DateService.monthDay(item)) {
    const result = await query.toArray({ cacheSeconds });
    let datasets = null;
    const labels: string[] = [];
    for (const { x , ... all } of result) {
        if (!datasets) {
            datasets = [];
        }
        let index = 0;
        for (const key in options) {
            if (key === "x") {
                continue;
            }
            if (Object.prototype.hasOwnProperty.call(options, key)) {
                const element = all[key] || undefined;
                const { data } = datasets[index++] ??= { ... options[key], data: [] };
                data.push(element);
            }
        }
        labels.push(label(x));
    }
    return {
        type: "line",
        data: {
            labels,
            datasets
        },
        options: {
            fullSize: false,
            interaction: {
                mode: "index",
                intersect: false
            },
            plugins: {
                title: {
                    display: !!title,
                    text: title
                }
            },
            scales: {
                x: {
                    ticks: {
                        callback(val, index) {
                            // console.log(this);
                            return (index === 0 || index === this.max)
                                ? this.getLabelForValue(val)
                                : "";
                        }
                    },
                    grid: {
                        display: false
                    }
                },
                y: {
                    grid: {
                        display: false
                    }
                }

            }
        }
    };
}
