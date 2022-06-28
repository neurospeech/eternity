import DateTime from "@web-atoms/date-time/dist/DateTime";

const formatter = new (Intl as any).RelativeTimeFormat(undefined, {
    numeric: 'auto'
});

const DIVISIONS = [
    { amount: 60, name: 'seconds' },
    { amount: 60, name: 'minutes' },
    { amount: 24, name: 'hours' },
    { amount: 7, name: 'days' },
    { amount: 4.34524, name: 'weeks' },
    { amount: 12, name: 'months' },
    { amount: Number.POSITIVE_INFINITY, name: 'years' }
];

function formatTimeAgo(date, from?) {
    from ??= new Date();
    let duration = (date - (from.getTime())) / 1000;

    for (let i = 0; i <= DIVISIONS.length; i++) {
        const division = DIVISIONS[i];
        if (Math.abs(duration) < division.amount) {
            return formatter.format(Math.round(duration), division.name);
        }
        duration /= division.amount;
    }
}

const formats: Record<string, Intl.DateTimeFormat> = {};

export default class DateService {

    public static monthDay(d: Date | DateTime, def: string = ""): string {
        return d?.toLocaleDateString(navigator.language, {
            month: "short",
            day: "numeric"
        }) ?? def;
    }


    public static toStringWithWeekDay(d: Date | DateTime, def: string = ""): string {
        return d?.toLocaleDateString(navigator.language, {
            year: "numeric",
            month: "short",
            day: "numeric",
            weekday: "short"
        }) ?? def;
    }

    public static relative(
        d: Date | DateTime,
        prefix: string = ""): string {
        if (!d) {
            return "";
        }
        if (!(d instanceof DateTime)) {
            d = typeof d === "string" ? new DateTime(d) : DateTime.from(d);
        }
        const today = DateTime.now;
        if (today.dateEquals(d)) {
            return `${prefix} ${formatTimeAgo(d)}`;
        }
        const yesterday = today.addDays(-1);
        if (yesterday.dateEquals(d)) {
            return `${prefix} Yesterday ${formatTimeAgo(d, yesterday)}`;
        }
        return prefix + this.toStringWithWeekDay(d);
    }

    public static toStringWithWeekDayAndTime(d: Date | DateTime, def: string = ""): string {
        return d?.toLocaleString(navigator.language, {
            year: "numeric",
            month: "short",
            day: "numeric",
            weekday: "short",
            hour12: true,
            minute: "numeric",
            hour: "numeric"
        }) ?? def;
    }

    public static shortDateTime(d: Date | DateTime, def: string = ""): string {
        if (!d) {
            return def;
        }
        const today = new Date();
        const format: Intl.DateTimeFormatOptions = {
            day: "numeric",
            weekday: "short",
            hour12: true,
            minute: "2-digit",
            hour: "numeric",
            month: undefined
        };
        if (today.getMonth() !== (d as Date).getMonth()) {
            format.month = "short";
        }
        const dtf = new Intl.DateTimeFormat(navigator.language, format);
        const parts = (dtf as any).formatToParts(d);
        return parts.map(({type, value}, i) => {
            if (type === "literal" && value === ":") {
                return "";
            }
            if (type === "minute" || type === "second") {
                if (value === "00") {
                    return "";
                }
                return parts[i-1].value + value;
            }
            return value;
        } ).join("");
    }

    public static toTimeString(d: DateTime): string {
        return d.time.toString(true);
    }
}
