package com.horizon.media;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.media.Image;
import android.media.MediaCodec;
import android.media.MediaCodecInfo;
import android.media.MediaCodecList;
import android.media.MediaExtractor;
import android.media.MediaFormat;
import android.media.MediaMuxer;
import java.io.File;
import java.io.FileInputStream;
import java.nio.ByteBuffer;

/** Encodes recorded game frames. It neither reads nor changes game rules. */
public final class TimelineEncoder {
  private static final int WIDTH = 540, HEIGHT = 960, RATE = 22050;
  private static final class Drain {
    final MediaCodec codec; final MediaMuxer muxer;
    final MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();
    int track = -1; boolean started, ended;
    Drain(MediaCodec codec, String path) throws Exception { this.codec = codec; muxer = new MediaMuxer(path, MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4); }
    void pull(boolean finish) throws Exception {
      long deadline = System.currentTimeMillis() + 15000;
      do {
        int index = codec.dequeueOutputBuffer(info, finish ? 10000 : 0);
        if (index == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) { track = muxer.addTrack(codec.getOutputFormat()); muxer.start(); started = true; }
        else if (index >= 0) {
          ByteBuffer data = codec.getOutputBuffer(index);
          if ((info.flags & MediaCodec.BUFFER_FLAG_CODEC_CONFIG) != 0) info.size = 0;
          if (info.size > 0) { if (!started) throw new Exception("format_not_ready"); data.position(info.offset); data.limit(info.offset + info.size); muxer.writeSampleData(track, data, info); }
          ended = (info.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0; codec.releaseOutputBuffer(index, false);
        } else if (!finish) break;
        if (System.currentTimeMillis() > deadline) throw new Exception("encoder_timeout");
      } while (finish && !ended || !finish && !ended);
    }
    void close() { try { if (started) muxer.stop(); } finally { muxer.release(); codec.stop(); codec.release(); } }
    int input() throws Exception {
      long deadline = System.currentTimeMillis() + 15000;
      while (true) { int index = codec.dequeueInputBuffer(10000); if (index >= 0) return index; pull(false); if (System.currentTimeMillis() > deadline) throw new Exception("encoder_timeout"); }
    }
  }
  private static int clamp(int value) { return Math.max(0, Math.min(255, value)); }
  private static int y(int pixel) { int r = pixel >> 16 & 255, g = pixel >> 8 & 255, b = pixel & 255; return clamp(((66*r + 129*g + 25*b + 128) >> 8) + 16); }
  private static int u(int pixel) { int r = pixel >> 16 & 255, g = pixel >> 8 & 255, b = pixel & 255; return clamp(((-38*r - 74*g + 112*b + 128) >> 8) + 128); }
  private static int v(int pixel) { int r = pixel >> 16 & 255, g = pixel >> 8 & 255, b = pixel & 255; return clamp(((112*r - 94*g - 18*b + 128) >> 8) + 128); }
  private static void imageFrame(Image image, int[] pixels) {
    Image.Plane[] planes = image.getPlanes();
    for (int p = 0; p < 3; p++) {
      ByteBuffer bytes = planes[p].getBuffer(); int row = planes[p].getRowStride(), stride = planes[p].getPixelStride();
      int width = p == 0 ? WIDTH : WIDTH / 2, height = p == 0 ? HEIGHT : HEIGHT / 2;
      for (int yy = 0; yy < height; yy++) for (int xx = 0; xx < width; xx++) {
        int pixel = pixels[(p == 0 ? yy : yy*2) * WIDTH + (p == 0 ? xx : xx*2)];
        bytes.put(yy * row + xx * stride, (byte)(p == 0 ? y(pixel) : p == 1 ? u(pixel) : v(pixel)));
      }
    }
  }
  private static void byteFrame(ByteBuffer bytes, int[] pixels, boolean semiplanar) {
    bytes.clear(); for (int pixel : pixels) bytes.put((byte)y(pixel));
    if (semiplanar) for (int yy = 0; yy < HEIGHT; yy += 2) for (int xx = 0; xx < WIDTH; xx += 2) { int pixel = pixels[yy*WIDTH+xx]; bytes.put((byte)u(pixel)); bytes.put((byte)v(pixel)); }
    else for (int p = 1; p <= 2; p++) for (int yy = 0; yy < HEIGHT; yy += 2) for (int xx = 0; xx < WIDTH; xx += 2) { int pixel = pixels[yy*WIDTH+xx]; bytes.put((byte)(p == 1 ? u(pixel) : v(pixel))); }
  }
  private static void video(String directory, int count, int fps, String path) throws Exception {
    MediaCodec codec = null; int colour = -1;
    search: for (MediaCodecInfo candidate : new MediaCodecList(MediaCodecList.REGULAR_CODECS).getCodecInfos()) {
      if (!candidate.isEncoder()) continue;
      for (String type : candidate.getSupportedTypes()) if (type.equalsIgnoreCase("video/avc")) {
        int[] formats = candidate.getCapabilitiesForType(type).colorFormats;
        for (int preferred : new int[] { MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420Flexible, MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420Planar, MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420SemiPlanar })
          for (int available : formats) if (available == preferred) { codec = MediaCodec.createByCodecName(candidate.getName()); colour = preferred; break search; }
      }
    }
    if (codec == null) throw new Exception("h264_encoder_unavailable");
    MediaFormat format = MediaFormat.createVideoFormat("video/avc", WIDTH, HEIGHT);
    format.setInteger(MediaFormat.KEY_COLOR_FORMAT, colour); format.setInteger(MediaFormat.KEY_BIT_RATE, 2500000);
    format.setInteger(MediaFormat.KEY_FRAME_RATE, fps); format.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 1);
    codec.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE); codec.start(); Drain drain = new Drain(codec, path);
    try {
      int[] pixels = new int[WIDTH * HEIGHT];
      for (int frame = 0; frame < count; frame++) {
        Bitmap bitmap = BitmapFactory.decodeFile(new File(directory, String.format(java.util.Locale.ROOT, "frame-%03d.png", frame)).getPath());
        if (bitmap == null || bitmap.getWidth() != WIDTH || bitmap.getHeight() != HEIGHT) throw new Exception("invalid_frame");
        bitmap.getPixels(pixels, 0, WIDTH, 0, 0, WIDTH, HEIGHT); bitmap.recycle(); int index = drain.input();
        Image image = colour == MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420Flexible ? codec.getInputImage(index) : null;
        if (image != null) imageFrame(image, pixels); else byteFrame(codec.getInputBuffer(index), pixels, colour == MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420SemiPlanar);
        codec.queueInputBuffer(index, 0, WIDTH * HEIGHT * 3 / 2, frame * 1000000L / fps, 0); drain.pull(false);
      }
      int index = drain.input(); codec.queueInputBuffer(index, 0, 0, count * 1000000L / fps, MediaCodec.BUFFER_FLAG_END_OF_STREAM); drain.pull(true);
    } finally { drain.close(); }
  }
  private static void audio(String pcm, String path) throws Exception {
    MediaCodec codec = MediaCodec.createEncoderByType("audio/mp4a-latm"); MediaFormat format = MediaFormat.createAudioFormat("audio/mp4a-latm", RATE, 1);
    format.setInteger(MediaFormat.KEY_AAC_PROFILE, MediaCodecInfo.CodecProfileLevel.AACObjectLC); format.setInteger(MediaFormat.KEY_BIT_RATE, 64000);
    codec.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE); codec.start(); Drain drain = new Drain(codec, path);
    try (FileInputStream input = new FileInputStream(pcm)) {
      byte[] bytes = new byte[2048]; int offset = 0, size;
      while ((size = input.read(bytes)) > 0) { int index = drain.input(); ByteBuffer buffer = codec.getInputBuffer(index); buffer.clear(); buffer.put(bytes, 0, size);
        codec.queueInputBuffer(index, 0, size, (offset / 2) * 1000000L / RATE, 0); offset += size; drain.pull(false); }
      int index = drain.input(); codec.queueInputBuffer(index, 0, 0, (offset / 2) * 1000000L / RATE, MediaCodec.BUFFER_FLAG_END_OF_STREAM); drain.pull(true);
    } finally { drain.close(); }
  }
  private static void merge(String video, String audio, String output) throws Exception {
    MediaExtractor[] readers = { new MediaExtractor(), new MediaExtractor() }; MediaMuxer muxer = new MediaMuxer(output, MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4);
    boolean started = false;
    try {
      int[] tracks = new int[2]; for (int i = 0; i < 2; i++) { readers[i].setDataSource(i == 0 ? video : audio); readers[i].selectTrack(0); tracks[i] = muxer.addTrack(readers[i].getTrackFormat(0)); }
      muxer.start(); started = true; ByteBuffer buffer = ByteBuffer.allocate(4194304); MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();
      for (int i = 0; i < 2; i++) while (true) { buffer.clear(); int size = readers[i].readSampleData(buffer, 0); if (size < 0) break;
        info.set(0, size, readers[i].getSampleTime(), readers[i].getSampleFlags()); if (info.presentationTimeUs >= 0 && info.presentationTimeUs < 10000000) muxer.writeSampleData(tracks[i], buffer, info); readers[i].advance(); }
    } finally { for (MediaExtractor reader : readers) reader.release(); if (started) muxer.stop(); muxer.release(); }
  }
  public static String encode(String frames, int count, int fps, String pcm, String output) {
    String video = output + ".video.mp4", audio = output + ".audio.mp4";
    try { if (count != 60 || fps != 6) return "invalid_duration"; video(frames, count, fps, video); audio(pcm, audio); merge(video, audio, output); return ""; }
    catch (Exception failure) { new File(output).delete(); return "video_encoding_unavailable"; }
    finally { new File(video).delete(); new File(audio).delete(); }
  }
}
