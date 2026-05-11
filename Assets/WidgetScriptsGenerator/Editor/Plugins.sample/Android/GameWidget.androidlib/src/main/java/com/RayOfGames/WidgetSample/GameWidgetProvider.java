package com.RayOfGames.WidgetSample;

import android.app.PendingIntent;
import android.appwidget.AppWidgetManager;
import android.appwidget.AppWidgetProvider;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.util.TypedValue;
import android.widget.RemoteViews;

import org.json.JSONObject;

import java.io.File;
import java.io.FileInputStream;
import java.text.SimpleDateFormat;
import java.util.Calendar;
import java.util.Date;
import java.util.Locale;
import java.util.concurrent.TimeUnit;

/**
 * Android home-screen widget that shows the game character
 * and the player's login streak.
 *
 * Day boundary is 6:00 AM (not midnight). Before 6am still counts as the
 * previous day for login purposes.
 *
 * Image changes based on:
 *   - Whether the player logged in today (ok vs notok)
 *   - Current time slot (for ok images, always rotate):
 *       1 = 06:00–10:59
 *       2 = 11:00–18:59
 *       3 = 19:00–21:59
 *       4 = 22:00–05:59
 *   - For notok images:
 *       First missed day: rotate 1→2→3→4 based on time slot
 *       2+ missed days:   always show notok_4 (most severe)
 *
 * Drawable naming: widget_ok_1 .. widget_ok_4, widget_notok_1 .. widget_notok_4
 */
public class GameWidgetProvider extends AppWidgetProvider {

    private static final String WIDGET_DATA_FILE = "widget_data.json";

    /** Day boundary hour — 6:00 AM */
    private static final int DAY_BOUNDARY_HOUR = 6;

    // Text styling
    private static final int TEXT_COLOR = 0xFFFFEA84;
    private static final int SHADOW_COLOR = 0x80000000;
    private static final float SHADOW_DX = 2f;
    private static final float SHADOW_DY = 2f;
    private static final float SHADOW_RADIUS = 3f;
    private static final float TEXT_SIZE_SP = 24f;

    @Override
    public void onUpdate(Context context, AppWidgetManager appWidgetManager, int[] appWidgetIds) {
        for (int appWidgetId : appWidgetIds) {
            updateWidget(context, appWidgetManager, appWidgetId);
        }
    }

    @Override
    public void onReceive(Context context, Intent intent) {
        super.onReceive(context, intent);

        if (AppWidgetManager.ACTION_APPWIDGET_UPDATE.equals(intent.getAction())) {
            AppWidgetManager manager = AppWidgetManager.getInstance(context);
            int[] ids = manager.getAppWidgetIds(
                    new ComponentName(context, GameWidgetProvider.class));
            onUpdate(context, manager, ids);
        }
    }

    private void updateWidget(Context context, AppWidgetManager appWidgetManager, int appWidgetId) {
        int layoutId = getResId(context, "widget_layout", "layout");
        RemoteViews views = new RemoteViews(context.getPackageName(), layoutId);

        LoginStatus status = readLoginStatus(context);
        int timeSlot = getCurrentTimeSlot();

        // Determine which drawable to show
        String drawableName;
        if (status.loggedInToday) {
            // OK images always rotate with time slot
            drawableName = "widget_ok_" + timeSlot;
        } else {
            if (status.daysSinceLogin <= 1) {
                // First missed day: rotate notok 1→2→3→4
                drawableName = "widget_notok_" + timeSlot;
            } else {
                // 2+ missed days: lock to notok_4 (most severe)
                drawableName = "widget_notok_4";
            }
        }

        views.setImageViewResource(
                getResId(context, "widget_image", "id"),
                getResId(context, drawableName, "drawable"));

        // Render streak text as bitmap with Lilita One font
        if (status.streak > 0) {
            Bitmap textBitmap = renderStreakText(context, status.streak + "🔥");
            views.setImageViewBitmap(
                    getResId(context, "widget_text_image", "id"),
                    textBitmap);
        } else {
            views.setImageViewBitmap(
                    getResId(context, "widget_text_image", "id"),
                    null);
        }

        // Tapping the widget opens the game
        Intent launchIntent = context.getPackageManager()
                .getLaunchIntentForPackage(context.getPackageName());
        if (launchIntent != null) {
            PendingIntent pendingIntent = PendingIntent.getActivity(
                    context, 0, launchIntent,
                    PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
            views.setOnClickPendingIntent(getResId(context, "widget_root", "id"), pendingIntent);
        }

        appWidgetManager.updateAppWidget(appWidgetId, views);
    }

    /**
     * Renders the streak text as a Bitmap using the Lilita One custom font,
     * with the golden color and drop shadow.
     */
    private Bitmap renderStreakText(Context context, String text) {
        float textSizePx = TypedValue.applyDimension(
                TypedValue.COMPLEX_UNIT_SP, TEXT_SIZE_SP,
                context.getResources().getDisplayMetrics());

        Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        paint.setTextSize(textSizePx);
        paint.setColor(TEXT_COLOR);
        paint.setTextAlign(Paint.Align.LEFT);
        paint.setShadowLayer(SHADOW_RADIUS, SHADOW_DX, SHADOW_DY, SHADOW_COLOR);

        // Load Lilita One font from res/font
        try {
            Typeface lilitaOne = context.getResources().getFont(
                    getResId(context, "lilita_one", "font"));
            paint.setTypeface(lilitaOne);
        } catch (Exception e) {
            paint.setTypeface(Typeface.DEFAULT_BOLD);
        }

        // Measure text bounds
        float textWidth = paint.measureText(text);
        Paint.FontMetrics fm = paint.getFontMetrics();
        float textHeight = fm.descent - fm.ascent;

        // Add padding for the shadow
        int padding = (int) (SHADOW_RADIUS + Math.max(SHADOW_DX, SHADOW_DY)) + 2;
        int bmpWidth = (int) Math.ceil(textWidth) + padding * 2;
        int bmpHeight = (int) Math.ceil(textHeight) + padding * 2;

        Bitmap bitmap = Bitmap.createBitmap(bmpWidth, bmpHeight, Bitmap.Config.ARGB_8888);
        Canvas canvas = new Canvas(bitmap);
        canvas.drawText(text, padding, -fm.ascent + padding, paint);

        return bitmap;
    }

    /**
     * Returns the current time slot (based on real clock, not shifted):
     *   1 = 06:00–10:59
     *   2 = 11:00–18:59
     *   3 = 19:00–21:59
     *   4 = 22:00–05:59
     */
    private int getCurrentTimeSlot() {
        int hour = Calendar.getInstance().get(Calendar.HOUR_OF_DAY);
        if (hour >= 6 && hour < 11) return 1;
        if (hour >= 11 && hour < 19) return 2;
        if (hour >= 19 && hour < 22) return 3;
        return 4; // 22–5
    }

    /**
     * Returns the "effective date" string for login purposes.
     * Day boundary is 6:00 AM — before 6am counts as the previous day.
     * We shift the current time back by 6 hours, then take the date.
     */
    private String getEffectiveDate() {
        Calendar cal = Calendar.getInstance();
        cal.add(Calendar.HOUR_OF_DAY, -DAY_BOUNDARY_HOUR);
        return new SimpleDateFormat("yyyy-MM-dd", Locale.US).format(cal.getTime());
    }

    /**
     * Reads widget_data.json and determines login status.
     * Uses the 6am day boundary for all date comparisons.
     */
    private LoginStatus readLoginStatus(Context context) {
        try {
            File file = new File(context.getFilesDir(), WIDGET_DATA_FILE);
            if (!file.exists()) {
                File extDir = context.getExternalFilesDir(null);
                if (extDir != null) {
                    file = new File(extDir, WIDGET_DATA_FILE);
                }
            }
            if (!file.exists()) {
                return new LoginStatus(false, 0, 999);
            }

            FileInputStream fis = new FileInputStream(file);
            byte[] data = new byte[(int) file.length()];
            fis.read(data);
            fis.close();
            String content = new String(data, "UTF-8");

            JSONObject json = new JSONObject(content);
            String lastLoginDate = json.optString("lastLoginDate", "");
            int streak = json.optInt("streak", 0);

            String effectiveToday = getEffectiveDate();
            boolean loggedInToday = lastLoginDate.equals(effectiveToday);

            // Calculate days since last login (using effective dates)
            long daysSince = 999;
            if (!lastLoginDate.isEmpty()) {
                SimpleDateFormat sdf = new SimpleDateFormat("yyyy-MM-dd", Locale.US);
                Date lastDate = sdf.parse(lastLoginDate);
                Date todayDate = sdf.parse(effectiveToday);
                long diffMs = todayDate.getTime() - lastDate.getTime();
                daysSince = TimeUnit.MILLISECONDS.toDays(diffMs);
            }

            return new LoginStatus(loggedInToday, streak, daysSince);
        } catch (Exception e) {
            e.printStackTrace();
            return new LoginStatus(false, 0, 999);
        }
    }

    private int getResId(Context context, String name, String type) {
        return context.getResources().getIdentifier(name, type, context.getPackageName());
    }

    private static class LoginStatus {
        final boolean loggedInToday;
        final int streak;
        final long daysSinceLogin;

        LoginStatus(boolean loggedInToday, int streak, long daysSinceLogin) {
            this.loggedInToday = loggedInToday;
            this.streak = streak;
            this.daysSinceLogin = daysSinceLogin;
        }
    }
}
