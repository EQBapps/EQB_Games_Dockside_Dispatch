package com.eqbbusiness.dockside;

import android.app.Activity;
import android.util.Log;

import com.google.android.gms.tasks.OnCompleteListener;
import com.google.android.gms.tasks.Task;
import com.google.android.play.core.review.ReviewInfo;
import com.google.android.play.core.review.ReviewManager;
import com.google.android.play.core.review.ReviewManagerFactory;

/**
 * Google Play In-App Review, called from C# (ReviewPrompt.cs). Play decides whether the rating card is actually shown
 * (it limits how often) and never says whether the player rated; the callback only reports that the flow has finished.
 */
public final class ReviewBridge {
    private static final String TAG = "Dockside";

    /** Implemented in C# as an AndroidJavaProxy. Called on the Android UI thread. */
    public interface Callback {
        /** The request succeeded and the card is being launched (Play may still choose not to show it). */
        void launching();
        /** The flow is over: ok is false when the request failed (error says why). */
        void done(boolean ok, String error);
    }

    private ReviewBridge() {}

    public static void request(final Activity activity, final Callback callback) {
        activity.runOnUiThread(new Runnable() {
            @Override public void run() {
                try {
                    final ReviewManager manager = ReviewManagerFactory.create(activity);
                    manager.requestReviewFlow().addOnCompleteListener(new OnCompleteListener<ReviewInfo>() {
                        @Override public void onComplete(Task<ReviewInfo> request) {
                            if (!request.isSuccessful()) {
                                Exception e = request.getException();
                                Log.w(TAG, "In-app review request failed: " + e);
                                callback.done(false, String.valueOf(e));
                                return;
                            }
                            callback.launching();
                            manager.launchReviewFlow(activity, request.getResult()).addOnCompleteListener(new OnCompleteListener<Void>() {
                                @Override public void onComplete(Task<Void> launch) {
                                    callback.done(true, null);
                                }
                            });
                        }
                    });
                } catch (Throwable t) {
                    Log.w(TAG, "In-app review failed: " + t);
                    callback.done(false, String.valueOf(t));
                }
            }
        });
    }
}
