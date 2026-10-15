async function hangCurrentPage() {
  const heading = document.getElementById("heading");
  const status = document.getElementById("status");
  try {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (!tab?.url || !/^https?:\/\//i.test(tab.url)) {
      throw new Error("打开一个普通网页再试试。Chrome 设置页和本地文件暂时不能挂上去。");
    }
    document.getElementById("title").textContent = tab.title || new URL(tab.url).hostname;
    const videoFrame = await findVisibleVideo(tab.id);
    const screenshotURL = await chrome.tabs.captureVisibleTab(tab.windowId, { format: "png" });
    const screenshot = videoFrame ? await cropScreenshot(screenshotURL, videoFrame) : screenshotURL;
    const response = await chrome.runtime.sendNativeMessage("app.hanglater.capture", {
      url: tab.url,
      title: tab.title || new URL(tab.url).hostname,
      screenshot: screenshot.replace(/^data:image\/png;base64,/, "")
    });
    if (!response?.ok) throw new Error(response?.error || "这次没能保存，请再试一次。");
    heading.textContent = "已经晾好。";
    status.textContent = videoFrame
      ? "已保存视频播放器画面。Hang Later 会在一秒内收下，点卡片就能回到这一页。"
      : "已送到这台电脑，Hang Later 会在一秒内收下。等你有空，点卡片就能回到这一页。";
    await chrome.action.setBadgeText({ text: "✓", tabId: tab.id });
    await chrome.action.setBadgeBackgroundColor({ color: "#000000", tabId: tab.id });
  } catch (error) {
    heading.textContent = "还差一步。";
    const nativeFailure = /native|host|access.*forbidden/i.test(error.message || "");
    status.textContent = nativeFailure
      ? "先打开 Hang Later →「连接 Chrome」→ 点击「连接 Chrome」，再回来试一次。"
      : error.message || "没有保存成功，请再试一次。";
    status.classList.add("error");
  }
}

async function findVisibleVideo(tabId) {
  try {
    const [execution] = await chrome.scripting.executeScript({
      target: { tabId },
      func: () => {
        const candidates = [...document.querySelectorAll("video")].map((video) => {
          const rect = video.getBoundingClientRect();
          const style = getComputedStyle(video);
          const left = Math.max(0, rect.left);
          const top = Math.max(0, rect.top);
          const right = Math.min(window.innerWidth, rect.right);
          const bottom = Math.min(window.innerHeight, rect.bottom);
          const width = Math.max(0, right - left);
          const height = Math.max(0, bottom - top);
          const visible = style.display !== "none" && style.visibility !== "hidden" && Number(style.opacity) > 0;
          return { left, top, width, height, area: visible ? width * height : 0 };
        }).filter((item) => item.width >= 240 && item.height >= 135);
        const best = candidates.sort((a, b) => b.area - a.area)[0];
        if (!best) return null;
        return { ...best, viewportWidth: window.innerWidth, viewportHeight: window.innerHeight };
      }
    });
    return execution?.result || null;
  } catch (_) {
    return null;
  }
}

async function cropScreenshot(dataURL, frame) {
  const blob = await (await fetch(dataURL)).blob();
  const image = await createImageBitmap(blob);
  const scaleX = image.width / Math.max(frame.viewportWidth, 1);
  const scaleY = image.height / Math.max(frame.viewportHeight, 1);
  const sourceX = Math.max(0, Math.round(frame.left * scaleX));
  const sourceY = Math.max(0, Math.round(frame.top * scaleY));
  const sourceWidth = Math.min(image.width - sourceX, Math.round(frame.width * scaleX));
  const sourceHeight = Math.min(image.height - sourceY, Math.round(frame.height * scaleY));
  if (sourceWidth < 240 || sourceHeight < 135) return dataURL;
  const canvas = document.createElement("canvas");
  canvas.width = sourceWidth;
  canvas.height = sourceHeight;
  canvas.getContext("2d").drawImage(image, sourceX, sourceY, sourceWidth, sourceHeight, 0, 0, sourceWidth, sourceHeight);
  return canvas.toDataURL("image/png");
}
hangCurrentPage();
