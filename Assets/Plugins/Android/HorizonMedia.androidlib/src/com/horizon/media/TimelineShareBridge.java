package com.horizon.media;
import android.app.Activity;
import android.content.Intent;
import android.content.ClipData;
import android.net.Uri;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
public final class TimelineShareBridge {
  public static boolean publish(final Activity activity, String path) {
    try {
      File source = new File(path);
      if (!source.isFile() || !source.getName().matches("[A-Za-z0-9_.-]+\\.(mp4|gif)")) return false;
      File directory = new File(activity.getCacheDir(), "HorizonShares"); directory.mkdirs();
      File target = new File(directory, source.getName());
      try (FileInputStream in = new FileInputStream(source); FileOutputStream out = new FileOutputStream(target)) {
        byte[] buffer = new byte[65536]; int count;
        while ((count = in.read(buffer)) != -1) out.write(buffer, 0, count);
      }
      final Uri uri = Uri.parse("content://" + activity.getPackageName() + ".horizonshare/" + target.getName());
      final String type = target.getName().endsWith(".mp4") ? "video/mp4" : "image/gif";
      activity.runOnUiThread(new Runnable() { public void run() {
        Intent send = new Intent(Intent.ACTION_SEND).setType(type).putExtra(Intent.EXTRA_STREAM, uri);
        send.setClipData(ClipData.newRawUri("HORIZON timeline", uri)); send.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        activity.startActivity(Intent.createChooser(send, "分享你的时间线"));
      }});
      return true;
    } catch (Exception failure) { return false; }
  }
}
