// Calculates the number of days until the given date
module.exports.daysUntil = function(month, day) {
    // Get today's date (disregarding time)
    var today = new Date();
    today.setHours(0, 0, 0, 0);

    // Get target date (disregarding time)
    var thisYear = today.getFullYear();
    var targetDate = new Date(thisYear, month - 1, day); // Month is 0-based
    targetDate.setHours(0, 0, 0, 0);

    // If target date already happened this year, move target date to next year
    if (targetDate < today) {
        targetDate.setFullYear(thisYear + 1);
    }

    var msInDay = 1000 * 60 * 60 * 24;
    return Math.round((targetDate - today) / msInDay);
};