package local.pocketdrop.android;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.TextView;
import java.io.*;
import java.lang.reflect.Method;
import org.junit.Test;
import org.junit.runner.RunWith;
import org.robolectric.Robolectric;
import org.robolectric.annotation.Config;
import org.robolectric.annotation.GraphicsMode;
import static org.junit.Assert.*;

/** Actual Android view rendering; no LAN, camera, updates or pairing vault. */
@RunWith(org.robolectric.RobolectricTestRunner.class)
@Config(sdk=28,qualifiers="mdpi")
@GraphicsMode(GraphicsMode.Mode.NATIVE)
public class MobileUiTest {
    private MainActivity screen()throws Exception{
        MainActivity a=Robolectric.buildActivity(MainActivity.class).get();
        Method build=MainActivity.class.getDeclaredMethod("buildUi");build.setAccessible(true);build.invoke(a);return a;
    }
    private void page(MainActivity a,int index)throws Exception{Method show=MainActivity.class.getDeclaredMethod("showTab",int.class);show.setAccessible(true);show.invoke(a,index);}
    private View root(MainActivity a){return ((ViewGroup)a.findViewById(android.R.id.content)).getChildAt(0);}
    private void render(MainActivity a,int width,int height,String name)throws Exception{
        View v=root(a);v.measure(View.MeasureSpec.makeMeasureSpec(width,View.MeasureSpec.EXACTLY),View.MeasureSpec.makeMeasureSpec(height,View.MeasureSpec.EXACTLY));v.layout(0,0,width,height);
        Bitmap image=Bitmap.createBitmap(width,height,Bitmap.Config.ARGB_8888);v.draw(new Canvas(image));
        File dir=new File("build/reports/mobile-ui");assertTrue(dir.isDirectory()||dir.mkdirs());try(FileOutputStream out=new FileOutputStream(new File(dir,name+".png"))){assertTrue(image.compress(Bitmap.CompressFormat.PNG,100,out));}image.recycle();checkButtons(v);
    }
    private void checkButtons(View v){
        if(v instanceof Button&&v.getVisibility()==View.VISIBLE){assertTrue("Short touch target: "+((Button)v).getText(),v.getHeight()>=48);assertTrue("Narrow touch target",v.getWidth()>=48);}
        if(v instanceof ViewGroup)for(int i=0;i<((ViewGroup)v).getChildCount();i++)checkButtons(((ViewGroup)v).getChildAt(i));
    }
    private boolean has(View v,String text){if(v instanceof TextView&&((TextView)v).getText().toString().contains(text))return true;if(v instanceof ViewGroup)for(int i=0;i<((ViewGroup)v).getChildCount();i++)if(has(((ViewGroup)v).getChildAt(i),text))return true;return false;}
    @Test public void smallPhonePagesAndQrEntry()throws Exception{
        MainActivity a=screen();for(int tab=0;tab<3;tab++){page(a,tab);render(a,375,812,"phone-"+tab);assertTrue(has(root(a),"掃碼"));assertTrue(has(root(a),"0"+(tab+1)));}assertTrue(has(root(a),"掃描電腦連線 QR"));
    }
    @Test public void narrowLandscapeTabletAndLargeText()throws Exception{
        org.robolectric.RuntimeEnvironment.setQualifiers("w320dp-h640dp-port-mdpi");MainActivity a=screen();page(a,0);render(a,320,640,"narrow-text");
        org.robolectric.RuntimeEnvironment.setQualifiers("w812dp-h375dp-land-mdpi");a=screen();page(a,2);render(a,812,375,"landscape-devices");
        org.robolectric.RuntimeEnvironment.setQualifiers("w768dp-h1024dp-port-mdpi");a=screen();page(a,2);render(a,768,1024,"tablet-devices");
        org.robolectric.RuntimeEnvironment.setQualifiers("w375dp-h812dp-port-mdpi");a=screen();
        android.content.res.Configuration config=new android.content.res.Configuration(a.getResources().getConfiguration());config.fontScale=1.5f;a.getResources().updateConfiguration(config,a.getResources().getDisplayMetrics());
        Method build=MainActivity.class.getDeclaredMethod("buildUi");build.setAccessible(true);build.invoke(a);page(a,2);render(a,375,812,"large-text-devices");
    }
}
