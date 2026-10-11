chrome.runtime.onInstalled.addListener(async () => {
  await chrome.contextMenus.removeAll();
  chrome.contextMenus.create({ id: "hang-link", title: "晾到待办绳上", contexts: ["link"], targetUrlPatterns: ["http://*/*", "https://*/*"] });
});

chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  if (info.menuItemId !== "hang-link" || !info.linkUrl || !/^https?:\/\//i.test(info.linkUrl)) return;
  try {
    const url = new URL(info.linkUrl);
    const response = await chrome.runtime.sendNativeMessage("app.hanglater.capture", {
      title: url.hostname, url: url.href
    });
    if (!response?.ok) throw new Error(response?.error || "保存失败");
    await chrome.action.setBadgeText({ text: "✓", tabId: tab.id });
    await chrome.action.setBadgeBackgroundColor({ color: "#000000", tabId: tab.id });
    await chrome.action.setTitle({ title: "已晾好这个链接", tabId: tab.id });
  } catch (error) {
    await chrome.action.setBadgeText({ text: "!", tabId: tab.id });
    await chrome.action.setBadgeBackgroundColor({ color: "#000000", tabId: tab.id });
    await chrome.action.setTitle({ title: "请在 Hang Later 中连接 Chrome，然后重试。" , tabId: tab.id });
  }
});
