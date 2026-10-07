package local.pocketdrop.android;

import android.content.Context;
import android.content.res.ColorStateList;
import android.graphics.*;
import android.graphics.drawable.Drawable;
import android.graphics.drawable.RippleDrawable;
import android.view.Gravity;
import android.view.View;
import android.widget.*;

/** Code-drawn geometry and real native text. No bitmap UI, blur or render loop. */
final class EndfieldUi {
    static final int BG=0xff191919,SURFACE=0xff202426,RAISED=0xff35373c,
        TEXT=0xfff0f2f3,MUTED=0xffb7c0c5,LINE=0xff747c80,ACCENT=0xfffffa00;
    private final Context context;
    private final float density;
    final Typeface display;
    EndfieldUi(Context c){context=c;density=c.getResources().getDisplayMetrics().density;display=Typeface.createFromAsset(c.getAssets(),"RussoOne-Regular.ttf");}
    int dp(float n){return Math.round(n*density);}
    TextView label(String value,int size,int color){
        TextView t=new TextView(context);t.setText(value);t.setTextColor(color);t.setTextSize(size);t.setFontFeatureSettings("tnum");
        t.setPadding(0,dp(4),0,dp(4));t.setLineSpacing(dp(3),1);return t;
    }
    LinearLayout column(){LinearLayout v=new LinearLayout(context);v.setOrientation(LinearLayout.VERTICAL);return v;}
    LinearLayout card(){LinearLayout v=column();v.setPadding(dp(18),dp(16),dp(18),dp(18));v.setBackground(frame(SURFACE,true));return v;}
    LinearLayout.LayoutParams spaced(){LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,-2);p.bottomMargin=dp(16);return p;}
    Drawable frame(int color,boolean cut){return new Frame(color,LINE,cut);}
    Drawable buttonSurface(boolean primary){
        return new RippleDrawable(ColorStateList.valueOf(primary?0x33191919:0x40ffffff),new Frame(primary?ACCENT:RAISED,primary?ACCENT:LINE,true),null);
    }
    Button button(String value,Runnable action,boolean primary){
        Button b=new Button(context);b.setText(value);b.setAllCaps(false);b.setTextSize(15);b.setTypeface(Typeface.create("sans-serif-medium",Typeface.NORMAL));
        b.setTextColor(new ColorStateList(new int[][]{new int[]{-android.R.attr.state_enabled},new int[]{}},new int[]{0xff939a9e,primary?BG:TEXT}));
        b.setBackground(buttonSurface(primary));b.setMinHeight(dp(52));b.setMinimumHeight(dp(52));b.setPadding(dp(14),dp(10),dp(14),dp(10));
        b.setGravity(Gravity.CENTER);b.setOnClickListener(v->action.run());b.setStateListAnimator(null);
        LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,-2);p.topMargin=dp(8);b.setLayoutParams(p);return b;
    }
    View rule(){View line=new View(context);line.setBackgroundColor(LINE);LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,dp(1));p.topMargin=dp(12);p.bottomMargin=dp(12);line.setLayoutParams(p);line.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);return line;}
    TextView section(LinearLayout parent,String index,String title,String hint){
        TextView micro=label(index,12,MUTED);micro.setLetterSpacing(.08f);parent.addView(micro);
        TextView heading=label(title,24,TEXT);heading.setTypeface(Typeface.create("sans-serif",Typeface.BOLD));parent.addView(heading);
        if(!hint.isEmpty())parent.addView(label(hint,14,MUTED));parent.addView(rule());return heading;
    }
    final class Frame extends Drawable {
        final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG);final Path path=new Path();final int fill,border;final boolean cut;
        Frame(int fill,int border,boolean cut){this.fill=fill;this.border=border;this.cut=cut;}
        public void draw(Canvas c){
            Rect r=getBounds();float s=.5f*density,k=cut?dp(10):0;
            path.reset();path.moveTo(r.left+s+k,r.top+s);path.lineTo(r.right-s,r.top+s);path.lineTo(r.right-s,r.bottom-s-k);
            path.lineTo(r.right-s-k,r.bottom-s);path.lineTo(r.left+s,r.bottom-s);path.lineTo(r.left+s,r.top+s+k);path.close();
            paint.setStyle(Paint.Style.FILL);paint.setColor(fill);c.drawPath(path,paint);
            paint.setStyle(Paint.Style.STROKE);paint.setStrokeWidth(density);paint.setColor(border);c.drawPath(path,paint);
        }
        public void setAlpha(int alpha){paint.setAlpha(alpha);invalidateSelf();}public void setColorFilter(ColorFilter filter){paint.setColorFilter(filter);invalidateSelf();}
        public int getOpacity(){return PixelFormat.TRANSLUCENT;}
    }
}
