mergeInto(LibraryManager.library, {
  WataameVibrate: function (milliseconds) {
    if (typeof navigator !== "undefined" && navigator.vibrate) {
      navigator.vibrate(milliseconds);
    }
  },
});
