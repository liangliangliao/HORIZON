package com.horizon.media;
import android.content.ContentProvider;
import android.content.ContentValues;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;
import java.io.File;
import java.io.FileNotFoundException;
public final class TimelineShareProvider extends ContentProvider {
  public boolean onCreate() { return true; }
  private File file(Uri uri) throws FileNotFoundException {
    String name = uri.getLastPathSegment(); if (name == null || !name.matches("[A-Za-z0-9_.-]+\\.(mp4|gif)")) throw new FileNotFoundException();
    File file = new File(new File(getContext().getCacheDir(), "HorizonShares"), name); if (!file.isFile()) throw new FileNotFoundException(); return file;
  }
  public ParcelFileDescriptor openFile(Uri uri, String mode) throws FileNotFoundException { if (!"r".equals(mode)) throw new FileNotFoundException(); return ParcelFileDescriptor.open(file(uri), ParcelFileDescriptor.MODE_READ_ONLY); }
  public String getType(Uri uri) { return uri.toString().endsWith(".mp4") ? "video/mp4" : "image/gif"; }
  public Cursor query(Uri uri, String[] projection, String selection, String[] args, String order) {
    try { File file = file(uri); MatrixCursor cursor = new MatrixCursor(new String[] { OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE }); cursor.addRow(new Object[] { file.getName(), file.length() }); return cursor; }
    catch (FileNotFoundException failure) { return null; }
  }
  public Uri insert(Uri uri, ContentValues values) { throw new UnsupportedOperationException(); }
  public int update(Uri uri, ContentValues values, String selection, String[] args) { throw new UnsupportedOperationException(); }
  public int delete(Uri uri, String selection, String[] args) { throw new UnsupportedOperationException(); }
}
