package local.pocketdrop.android;

import android.app.*;
import android.content.*;
import android.database.Cursor;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.*;
import android.provider.MediaStore;
import android.provider.OpenableColumns;
import android.text.*;
import android.content.ClipboardManager;
import android.view.*;
import android.widget.*;
import androidx.core.content.FileProvider;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowInsetsCompat;
import com.google.zxing.integration.android.IntentIntegrator;
import com.google.zxing.integration.android.IntentResult;
import org.json.*;
import okhttp3.*;
import okio.BufferedSink;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.*;
import java.util.concurrent.*;

/** Foreground Android companion for the explicitly PC-hosted trial. */
public final class MainActivity extends Activity {
    private final Handler ui=new Handler(Looper.getMainLooper());
    private final ExecutorService network=Executors.newSingleThreadExecutor();
    private final ExecutorService transfers=Executors.newSingleThreadExecutor();
    private final ExecutorService polls=Executors.newSingleThreadExecutor();
    private final ExecutorService probes=Executors.newFixedThreadPool(2);
    private final HashSet<String> probing=new HashSet<>();
    private final EndpointCandidates candidates=new EndpointCandidates();
    private Call stateRequest;
    private long connectionEpoch;
    private long pairingAttempt;
    private Vault vault; private Nearby nearby; private AppUpdater appUpdater;
    private volatile RoomClient client; private WebSocket socket;
    private boolean foreground,online,editing,updating,transferBusy;
    private volatile boolean cancelled;
    private volatile Call transfer;
    private int tab=0, retryTicks;
    private long revision=-1;
    private JSONArray files=new JSONArray();
    private LinearLayout root,content,filesView,devicesView,textView;
    private TextView status,notice,progress,sectionNumber,dataCounter,charCount;
    private EndfieldUi art;
    private Button shareText;
    private boolean sendingText;
    private EditText editor;
    private Button cancel;
    private final Button[] tabButtons=new Button[3];
    private String pendingText;
    private final ArrayList<Uri> pendingUris=new ArrayList<>();
    private static final int PICK=31, INK=EndfieldUi.TEXT, MUTED=EndfieldUi.MUTED, ACCENT=EndfieldUi.ACCENT;
    private final Runnable pulse=new Runnable(){public void run(){if(!foreground)return;if(client!=null){refresh();connectSocket();if(!online){retryCandidates();if(++retryTicks%6==0)discover();}}ui.postDelayed(this,5000);}};

    @Override public void onCreate(Bundle saved){super.onCreate(saved);vault=new Vault(this);nearby=new Nearby(this);buildUi();
        try{JSONObject p=vault.load();if(p!=null)client=new RoomClient(p);}catch(Exception e){notice("無法讀取已保存的配對，請重新掃描");}
        if(saved==null)receive(getIntent());
        String draft=getSharedPreferences("update-draft",0).getString("text",null);
        if(draft!=null){editor.setText(draft);editing=true;}
        appUpdater=new AppUpdater(this,()->{
            if(transferBusy)throw new IOException("請先完成或取消檔案傳輸");
            var storage=getSharedPreferences("update-draft",0).edit();
            if(editing)storage.putString("text",editor.getText().toString());else storage.remove("text");
            if(!storage.commit())throw new IOException("無法保存文字草稿，請先分享後再更新");
        });
        renderDevices();showTab(0);// Updates are explicit while this integrated build is not published.
    }
    @Override protected void onResume(){super.onResume();if(appUpdater!=null)appUpdater.resume();}
    @Override public void onWindowFocusChanged(boolean focus){super.onWindowFocusChanged(focus);if(focus&&appUpdater!=null)appUpdater.resume();}
    @Override protected void onStart(){super.onStart();foreground=true;restartConnection();if(client!=null)consumePending();}
    @Override protected void onStop(){saveDraft();foreground=false;resetConnection();super.onStop();}
    @Override protected void onDestroy(){if(appUpdater!=null)appUpdater.close();cancelled=true;if(transfer!=null)transfer.cancel();network.shutdownNow();polls.shutdownNow();probes.shutdownNow();transfers.shutdownNow();super.onDestroy();}
    private void resetConnection(){connectionEpoch++;ui.removeCallbacks(pulse);nearby.stop();closeSocket();if(stateRequest!=null){stateRequest.cancel();stateRequest=null;}probing.clear();candidates.clear();}
    private void restartConnection(){
        resetConnection();retryTicks=0;setOnline(false);
        if(client!=null)try{client=new RoomClient(client.profile);}catch(Exception e){failure(e);}
        if(foreground){discover();ui.post(pulse);}
    }
    @Override protected void onNewIntent(Intent i){super.onNewIntent(i);setIntent(i);receive(i);consumePending();}
    private int dp(float n){return (int)(getResources().getDisplayMetrics().density*n+.5f);}
    private TextView label(String text,int size,int color){TextView t=new TextView(this);t.setText(text);t.setTextSize(size);t.setTextColor(color);t.setPadding(0,dp(6),0,dp(6));return t;}
    private android.graphics.drawable.Drawable surface(int color){return art.frame(color,true);}
    private LinearLayout column(){return art.column();}
    private Button button(String text,Runnable action){return art.button(text,action,false);}
    private Button primary(String text,Runnable action){return art.button(text,action,true);}
    private void scan(){new IntentIntegrator(this).setDesiredBarcodeFormats(IntentIntegrator.QR_CODE).setPrompt("掃描電腦共享籃框的連線 QR Code").setBeepEnabled(false).setOrientationLocked(false).initiateScan();}
    private void saveDraft(){if(editor==null)return;var draft=getSharedPreferences("update-draft",0).edit();if(editing)draft.putString("text",editor.getText().toString());else draft.remove("text");draft.apply();}
    private void counts(){if(dataCounter==null)return;dataCounter.setText(tab==0?"TEXT  /  "+editor.length()+" 字":tab==1?"FILES  /  "+files.length()+" 個":"ROOM  /  "+(client==null?"尚未配對":"配對已保存"));if(charCount!=null)charCount.setText(editor.length()+" / 32768 字");}
    private void buildUi(){
        art=new EndfieldUi(this);getWindow().setStatusBarColor(EndfieldUi.BG);getWindow().setNavigationBarColor(EndfieldUi.BG);
        boolean landscape=getResources().getConfiguration().orientation==android.content.res.Configuration.ORIENTATION_LANDSCAPE;
        int gutter=Math.max(20,(getResources().getConfiguration().screenWidthDp-680)/2);
        root=column();root.setBackgroundColor(EndfieldUi.BG);
        root.setPadding(dp(gutter),dp(landscape?4:12),dp(gutter),dp(8));
        ViewCompat.setOnApplyWindowInsetsListener(root,(v,insets)->{var bars=insets.getInsets(WindowInsetsCompat.Type.systemBars()|WindowInsetsCompat.Type.ime());v.setPadding(dp(gutter)+bars.left,dp(landscape?4:12)+bars.top,dp(gutter)+bars.right,dp(8)+bars.bottom);return insets;});
        TextView eyebrow=art.label("DESKTOP / MOBILE",12,MUTED);eyebrow.setLetterSpacing(.12f);root.addView(eyebrow);
        if(landscape)eyebrow.setVisibility(View.GONE);
        LinearLayout brand=new LinearLayout(this);brand.setGravity(Gravity.CENTER_VERTICAL);
        sectionNumber=art.label("01",40,INK);sectionNumber.setTypeface(art.display);sectionNumber.setMaxLines(1);sectionNumber.setAutoSizeTextTypeUniformWithConfiguration(18,40,1,android.util.TypedValue.COMPLEX_UNIT_SP);brand.addView(sectionNumber,new LinearLayout.LayoutParams(dp(72),dp(64)));
        TextView name=art.label("BASKETS",27,INK);name.setTypeface(art.display);name.setMaxLines(1);name.setAutoSizeTextTypeUniformWithConfiguration(16,27,1,android.util.TypedValue.COMPLEX_UNIT_SP);brand.addView(name,new LinearLayout.LayoutParams(0,dp(64),1));
        Button qr=primary("掃碼\n連線",this::scan);qr.setTextSize(13);qr.setMaxLines(2);qr.setAutoSizeTextTypeUniformWithConfiguration(10,13,1,android.util.TypedValue.COMPLEX_UNIT_SP);qr.setContentDescription("掃描電腦連線 QR Code");brand.addView(qr,new LinearLayout.LayoutParams(dp(72),dp(72)));root.addView(brand);
        status=art.label("尚未配對 · 與電腦連上同一個 Wi-Fi",13,MUTED);root.addView(status);
        dataCounter=art.label("TEXT / 0 字",12,MUTED);dataCounter.setTypeface(Typeface.MONOSPACE);root.addView(dataCounter);root.addView(art.rule());
        if(landscape){dataCounter.setVisibility(View.GONE);sectionNumber.getLayoutParams().height=dp(48);name.getLayoutParams().height=dp(48);qr.getLayoutParams().height=dp(48);qr.setText("掃碼");}
        ScrollView scroll=new ScrollView(this);scroll.setFillViewport(true);scroll.setClipToPadding(false);content=column();content.setPadding(0,0,0,dp(16));scroll.addView(content);root.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));
        notice=art.label("在桌面與手機之間，直接取用文字與檔案。",14,MUTED);notice.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
        textView=art.card();art.section(textView,"01 / SHARED TEXT","共享文字","收到的文字與未分享草稿，分開保留。");
        charCount=art.label("0 / 32768 字",12,MUTED);textView.addView(charCount);
        editor=new EditText(this);editor.setTextColor(INK);editor.setHintTextColor(MUTED);editor.setHint("貼上文字，按分享送到電腦");editor.setContentDescription("共享文字草稿");editor.setTextSize(17);editor.setGravity(Gravity.TOP);
        editor.setPadding(dp(12),dp(14),dp(12),dp(14));editor.setBackground(surface(EndfieldUi.BG));editor.setMinLines(5);editor.setMaxLines(14);
        editor.setInputType(android.text.InputType.TYPE_CLASS_TEXT|android.text.InputType.TYPE_TEXT_FLAG_MULTI_LINE|android.text.InputType.TYPE_TEXT_FLAG_CAP_SENTENCES);editor.setFilters(new InputFilter[]{new InputFilter.LengthFilter(32768)});textView.addView(editor,new LinearLayout.LayoutParams(-1,-2));
        editor.addTextChangedListener(new TextWatcher(){public void beforeTextChanged(CharSequence s,int st,int c,int a){}public void onTextChanged(CharSequence s,int st,int before,int count){if(!updating)editing=true;counts();}public void afterTextChanged(Editable s){}});
        shareText=primary("分享文字到電腦",()->sendText(editor.getText().toString()));textView.addView(shareText);
        LinearLayout row=new LinearLayout(this);Button copy=button("複製文字",()->{((ClipboardManager)getSystemService(CLIPBOARD_SERVICE)).setPrimaryClip(ClipData.newPlainText("Desktop Baskets",editor.getText()));notice("已複製");});
        LinearLayout.LayoutParams half=new LinearLayout.LayoutParams(0,-2,1);half.topMargin=dp(8);half.rightMargin=dp(8);row.addView(copy,half);
        row.addView(button("清空並同步",()->sendText("")),new LinearLayout.LayoutParams(0,-2,1));textView.addView(row);
        textView.addView(button("載入電腦最新文字",()->{editing=false;getSharedPreferences("update-draft",0).edit().remove("text").apply();refresh();}));
        filesView=column();devicesView=column();
        progress=art.label("",13,MUTED);progress.setVisibility(View.GONE);root.addView(progress);
        cancel=button("取消傳輸",()->{cancelled=true;Call c=transfer;if(c!=null)c.cancel();});cancel.setVisibility(View.GONE);root.addView(cancel);
        root.addView(notice);root.addView(art.rule());
        LinearLayout tabs=new LinearLayout(this);String[] names={"01  文字","02  檔案","03  裝置"};for(int i=0;i<3;i++){final int index=i;tabButtons[i]=button(names[i],()->showTab(index));tabButtons[i].setTextSize(14);LinearLayout.LayoutParams item=new LinearLayout.LayoutParams(0,-2,1);if(i<2)item.rightMargin=dp(8);tabs.addView(tabButtons[i],item);}root.addView(tabs);
        TextView footer=art.label("LAN ONLY   /   v"+BuildConfig.VERSION_NAME,12,MUTED);footer.setTypeface(Typeface.MONOSPACE);root.addView(footer);if(landscape)footer.setVisibility(View.GONE);setContentView(root);
    }
    private void showTab(int index){
        tab=index;sectionNumber.setText(String.format(Locale.ROOT,"%02d",index+1));
        for(int i=0;i<tabButtons.length;i++){boolean selected=i==index;tabButtons[i].setSelected(selected);tabButtons[i].setTextColor(selected?EndfieldUi.BG:INK);tabButtons[i].setBackground(art.buttonSurface(selected));tabButtons[i].setContentDescription(new String[]{"文字","檔案","裝置"}[i]+(selected?"，已選取":""));}
        content.removeAllViews();if(index==0)content.addView(textView);else if(index==1){renderFiles();content.addView(filesView);}else{renderDevices();content.addView(devicesView);}counts();
    }
    private void notice(String text){if(!isDestroyed())ui.post(()->notice.setText(text));}
    private void failure(Exception e){String m=e.getMessage();notice(m==null?"操作失敗，請確認 Wi-Fi 與電腦狀態":m);}
    private void setOnline(boolean value){boolean changed=online!=value;online=value;status.setText(client==null?"尚未配對 · 同一 Wi-Fi / LAN":value?"已連線 · 電腦共享籃框":"連線中斷 · 原配對保留，正在尋找電腦");status.setTextColor(value?ACCENT:MUTED);if(changed&&tab==1)renderFiles();counts();}
    private void discover(){
        RoomClient c=client;if(c==null||!foreground)return;long epoch=connectionEpoch;
        nearby.start(c.profile.optString("device_id"),endpoint->{
            if(!foreground||connectionEpoch!=epoch||client!=c)return;
            try{candidates.observe(endpoint,SystemClock.elapsedRealtime());}catch(IllegalArgumentException ignored){return;}
            if(!online||!endpoint.equals(c.endpoint))retryCandidates();
        });
    }
    private void retryCandidates(){
        RoomClient c=client;if(c==null||!foreground||probes.isShutdown())return;long epoch=connectionEpoch;
        for(String endpoint:candidates.due(SystemClock.elapsedRealtime(),Math.max(0,2-probing.size()),probing)){
            if(online&&endpoint.equals(c.endpoint))continue;
            probing.add(endpoint);
            probes.execute(()->{try{
                RoomClient candidate=c.at(endpoint);candidate.state();
                ui.post(()->{
                    if(!foreground||connectionEpoch!=epoch||client!=c)return;
                    try{vault.save(candidate.profile);}catch(Exception e){failure(e);return;}
                    resetConnection();client=candidate;setOnline(true);notice("✓ 已找到電腦，沿用原有配對");
                    discover();ui.post(pulse);
                });
            }catch(Exception ignored){/* Failed candidates remain eligible for the next foreground retry. */}
            finally{ui.post(()->{if(connectionEpoch==epoch)probing.remove(endpoint);});}});
        }
    }
    private void connectSocket(){RoomClient c=client;if(c==null || socket!=null || !foreground)return;
        socket=c.http.newWebSocket(c.request("/v1/events").build(),new WebSocketListener(){
            public void onMessage(WebSocket ws,String text){ui.post(()->{if(client==c&&socket==ws)refresh();});}
            public void onFailure(WebSocket ws,Throwable t,Response r){ui.post(()->{if(socket==ws){socket=null;refresh();}});}
            public void onClosing(WebSocket ws,int code,String reason){ws.close(code,null);ui.post(()->{if(socket==ws)socket=null;});}
            public void onClosed(WebSocket ws,int code,String reason){ui.post(()->{if(socket==ws)socket=null;});}
        });
    }
    private void closeSocket(){if(socket!=null){socket.cancel();socket=null;}}
    private void refresh(){
        if(Looper.myLooper()!=Looper.getMainLooper()){ui.post(this::refresh);return;}
        RoomClient c=client;if(!foreground||isDestroyed()||polls.isShutdown()||c==null||stateRequest!=null)return;
        long epoch=connectionEpoch;Call call=c.stateCall();stateRequest=call;
        polls.execute(()->{try{JSONObject state=RoomClient.read(call);ui.post(()->{
            if(connectionEpoch!=epoch||client!=c||!foreground)return;
            boolean wasOnline=online;setOnline(true);if(!wasOnline)notice("✓ 已沿用原有配對重新連線");
            JSONArray incoming=state.optJSONArray("files");if(incoming==null)incoming=new JSONArray();boolean filesChanged=!files.toString().equals(incoming.toString());files=incoming;long next=state.optLong("revision");
            if(!editing&&!editor.getText().toString().equals(state.optString("text"))){updating=true;editor.setText(state.optString("text"));updating=false;}
            else if(next!=revision&&revision>=0)notice("Room 有新文字；你的草稿已保留，可按「載入 Room 最新文字」");
            revision=next;if(tab==1&&filesChanged)renderFiles();counts();
        });}catch(Exception e){ui.post(()->{if(connectionEpoch==epoch&&client==c&&foreground){boolean wasOnline=online;setOnline(false);notice(RoomClient.connectionError(e));if(wasOnline){closeSocket();discover();}retryCandidates();}});}
        finally{ui.post(()->{if(stateRequest==call)stateRequest=null;});}});
    }
    private void sendText(String value){
        RoomClient c=client;if(c==null){notice("先掃描電腦的連線 QR Code");showTab(2);return;}if(sendingText)return;
        sendingText=true;shareText.setEnabled(false);shareText.setText("正在分享…");
        network.execute(()->{try{c.text(value);ui.post(()->{if(client!=c)return;if(editor.getText().toString().equals(value)||value.isEmpty()){editing=false;getSharedPreferences("update-draft",0).edit().remove("text").apply();updating=true;editor.setText(value);updating=false;}notice("文字已分享到電腦");refresh();});}catch(Exception e){failure(e);}finally{ui.post(()->{sendingText=false;if(!isDestroyed()){shareText.setEnabled(true);shareText.setText("分享文字到電腦");}});}});
    }
    private void renderDevices(){
        devicesView.removeAllViews();LinearLayout card=art.card();art.section(card,"03 / CONNECTION","連接電腦",client==null?"用手機掃描電腦顯示的連線 QR Code。":"原有配對已保存，回到同一網路即可連線。");
        card.addView(art.label(client==null?"01  電腦開啟共享籃框\n02  裝置 / QR → 顯示連線 QR\n03  按下方按鈕掃描":"ROOM  /  "+client.profile.optString("room_id").substring(0,Math.min(8,client.profile.optString("room_id").length())),15,MUTED));
        card.addView(primary(client==null?"掃描電腦連線 QR":"掃描新的連線 QR",this::scan));card.addView(button("使用八位驗證碼",this::pairByCode));
        if(client!=null){card.addView(button("重新尋找電腦",this::restartConnection));card.addView(button("忘記此 Room",()->new AlertDialog.Builder(this).setTitle("忘記此 Room？").setMessage("手機將移除配對。若要撤銷存取權，請在電腦移除此裝置。").setNegativeButton("取消",null).setPositiveButton("忘記",(d,w)->{cancelled=true;if(transfer!=null)transfer.cancel();pairingAttempt++;resetConnection();vault.forget();client=null;files=new JSONArray();revision=-1;editing=false;editor.setText("");saveDraft();nearby.stop();closeSocket();setOnline(false);renderDevices();}).show()));}
        devicesView.addView(card,art.spaced());LinearLayout info=art.card();art.section(info,"LOCAL / LAN","本機共享","Room 由電腦保存，電腦需保持開啟。手機端僅在開啟 App 時連線。");info.addView(button("檢查 App 更新",()->{if(appUpdater!=null)appUpdater.check();}));devicesView.addView(info);counts();
    }
    private void pairByCode(){
        LinearLayout panel=column();panel.setPadding(dp(18),dp(10),dp(18),dp(10));panel.addView(label("請在電腦按「顯示連線 QR」，再輸入八位驗證碼。",14,MUTED));
        Spinner rooms=new Spinner(this);ArrayList<String> names=new ArrayList<>(),addresses=new ArrayList<>();ArrayAdapter<String> adapter=new ArrayAdapter<>(this,android.R.layout.simple_spinner_dropdown_item,names);rooms.setAdapter(adapter);panel.addView(rooms);
        EditText code=new EditText(this);code.setHint("8 位驗證碼");code.setInputType(android.text.InputType.TYPE_CLASS_NUMBER);code.setFilters(new InputFilter[]{new InputFilter.LengthFilter(8)});panel.addView(code);
        TextView message=label("正在尋找附近的電腦…",13,MUTED);panel.addView(message);
        AlertDialog dialog=new AlertDialog.Builder(this).setTitle("驗證碼配對").setView(panel).setNegativeButton("取消",null).setPositiveButton("加入",null).create();
        dialog.setOnDismissListener(d->{nearby.stop();if(client!=null&&foreground)discover();});dialog.setOnShowListener(d->{nearby.start(null,result->{String[] parts=result.split("\\|",2);if(parts.length!=2||addresses.contains(parts[1])||names.size()>=32)return;names.add("共享電腦 · "+parts[0].substring(0,Math.min(8,parts[0].length())));addresses.add(parts[1]);adapter.notifyDataSetChanged();message.setText("選擇電腦，輸入它顯示的驗證碼。");});
            dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v->{if(addresses.isEmpty()){message.setText("尚未找到電腦，請確認同一個 Wi-Fi。");return;}String value=code.getText().toString();if(!value.matches("[0-9]{8}")){message.setText("請輸入 8 位驗證碼");return;}String endpoint=addresses.get(rooms.getSelectedItemPosition());long attempt=++pairingAttempt;dialog.getButton(AlertDialog.BUTTON_POSITIVE).setEnabled(false);message.setText("正在驗證電腦身分…");network.execute(()->{try{RoomClient paired=CodePairing.pair(endpoint,value,vault.deviceId(),Build.MODEL.substring(0,Math.min(60,Build.MODEL.length())),vault.publicKey());ui.post(()->{if(isDestroyed()||attempt!=pairingAttempt)return;try{vault.save(paired.profile);}catch(Exception e){failure(e);return;}client=paired;revision=-1;editing=false;dialog.dismiss();restartConnection();notice("✓ 已配對，之後會自動連線");renderDevices();consumePending();});}catch(Exception e){ui.post(()->{if(dialog.isShowing()){message.setText("配對失敗：請確認驗證碼，或在電腦產生新邀請。");dialog.getButton(AlertDialog.BUTTON_POSITIVE).setEnabled(true);}});}});});});dialog.show();
    }
    private void pair(String qr){long attempt=++pairingAttempt;network.execute(()->{try{
        if(qr.length()>4096)throw new IllegalArgumentException("QR Code 過大");JSONObject p=new JSONObject(qr);
        if(!"PocketDrop".equals(p.optString("app")) || p.optInt("protocol_version")!=1 || !"phone-trial".equals(p.optString("mode")))throw new IllegalArgumentException("不是 PocketDrop QR Code");
        UUID.fromString(p.getString("device_id"));UUID.fromString(p.getString("room_id"));if(!p.getString("token").matches("[A-Za-z0-9_-]{43}"))throw new IllegalArgumentException("邀請格式錯誤");
        RoomClient c=new RoomClient(p);JSONObject body=new JSONObject().put("token",p.getString("token")).put("device_id",vault.deviceId()).put("name",android.os.Build.MODEL.substring(0,Math.min(60,android.os.Build.MODEL.length()))).put("public_key",vault.publicKey());
        JSONObject result=c.json(new Request.Builder().url(c.endpoint+"/v1/pair").post(RequestBody.create(body.toString(),RoomClient.JSON)).build());
        if(!p.getString("room_id").equals(result.getString("room_id")) || !p.getString("device_id").equals(result.getString("device_id")) || !result.getString("credential").matches("[A-Za-z0-9_-]{43}"))throw new IllegalArgumentException("電腦回應不符配對資料");
        p.remove("token");p.put("credential",result.getString("credential"));RoomClient paired=new RoomClient(p);ui.post(()->{
            if(isDestroyed()||attempt!=pairingAttempt)return;
            try{vault.save(paired.profile);}catch(Exception e){failure(e);return;}
            client=paired;revision=-1;editing=false;restartConnection();notice("✓ 已配對，之後不用重掃");renderDevices();consumePending();
        });
    }catch(Exception e){failure(e);}});}
    @Override protected void onActivityResult(int request,int result,Intent data){super.onActivityResult(request,result,data);IntentResult scan=IntentIntegrator.parseActivityResult(request,result,data);if(scan!=null){if(scan.getContents()!=null)pair(scan.getContents());return;}if(request==PICK && result==RESULT_OK && data!=null){ArrayList<Uri> uris=new ArrayList<>();if(data.getClipData()!=null){for(int i=0;i<Math.min(100,data.getClipData().getItemCount());i++)uris.add(data.getClipData().getItemAt(i).getUri());}else if(data.getData()!=null)uris.add(data.getData());uploadAll(uris);}}
    @SuppressWarnings("deprecation") private void receive(Intent intent){
        if(intent==null)return;String action=intent.getAction();if(!Intent.ACTION_SEND.equals(action)&&!Intent.ACTION_SEND_MULTIPLE.equals(action))return;
        CharSequence text=intent.getCharSequenceExtra(Intent.EXTRA_TEXT);if(text!=null)pendingText=text.toString();
        if(Intent.ACTION_SEND.equals(action)){Uri uri=intent.getParcelableExtra(Intent.EXTRA_STREAM);if(uri!=null)pendingUris.add(uri);}else{ArrayList<Uri> uris=intent.getParcelableArrayListExtra(Intent.EXTRA_STREAM);if(uris!=null)pendingUris.addAll(uris.subList(0,Math.min(100,uris.size())));}
        if(client==null)notice("已收到分享內容，先掃描電腦 QR Code 即可加入 Room");
    }
    private void consumePending(){if(client==null)return;if(pendingText!=null){String text=pendingText;pendingText=null;updating=true;editor.setText(text);updating=false;sendText(text);showTab(0);}if(!pendingUris.isEmpty()&&!transferBusy){ArrayList<Uri> items=new ArrayList<>(pendingUris);pendingUris.clear();uploadAll(items);showTab(1);}}
    private void renderFiles(){
        filesView.removeAllViews();LinearLayout heading=art.card();art.section(heading,"02 / SHARED FILES","共享檔案",files.length()+" 個檔案 · 按需下載，原檔留在電腦。");
        Button add=primary("加入手機檔案",()->{if(client==null){showTab(2);notice("請先配對電腦");return;}Intent pick=new Intent(Intent.ACTION_OPEN_DOCUMENT).setType("*/*").addCategory(Intent.CATEGORY_OPENABLE).putExtra(Intent.EXTRA_ALLOW_MULTIPLE,true);startActivityForResult(pick,PICK);});add.setEnabled(!transferBusy);heading.addView(add);filesView.addView(heading,art.spaced());
        if(files.length()==0){LinearLayout empty=art.card();empty.addView(art.label("等待第一個檔案",20,INK));empty.addView(art.label("拖入電腦共享籃框，或從手機加入。只有按下載，才會保存手機副本。",15,MUTED));filesView.addView(empty);}
        for(int i=0;i<files.length();i++){JSONObject f=files.optJSONObject(i);if(f==null)continue;LinearLayout card=art.card();card.addView(art.label(String.format(Locale.ROOT,"%02d / FILE",i+1),12,MUTED));TextView filename=art.label(f.optString("name"),18,INK);filename.setTypeface(null,Typeface.BOLD);card.addView(filename);
            boolean available=online&&f.optBoolean("available");card.addView(art.label(size(f.optLong("size"))+" · "+f.optString("origin"),14,MUTED));card.addView(art.label(available?"可下載":"無法取得 · 來源離線或檔案已變更",13,available?ACCENT:MUTED));
            Button download=button("下載到手機",()->download(f));download.setEnabled(available&&!transferBusy);card.addView(download);filesView.addView(card,art.spaced());}counts();
    }
    private static String size(long n){if(n<1024)return n+" B";if(n<1024*1024)return String.format(Locale.ROOT,"%.1f KB",n/1024.0);if(n<1024L*1024*1024)return String.format(Locale.ROOT,"%.1f MB",n/(1024.0*1024));return String.format(Locale.ROOT,"%.1f GB",n/(1024.0*1024*1024));}
    private boolean beginTransfer(){if(client==null){notice("請先配對電腦");return false;}if(transferBusy){notice("請等待目前傳輸完成，或先取消");return false;}transferBusy=true;cancelled=false;cancel.setVisibility(View.VISIBLE);progress.setVisibility(View.VISIBLE);progress.setText("準備傳輸…");if(tab==1)renderFiles();return true;}
    private void endTransfer(){transfer=null;ui.post(()->{transferBusy=false;cancel.setVisibility(View.GONE);if(tab==1)renderFiles();});}
    private void updateProgress(String name,long done,long total,long started){long elapsed=Math.max(1,SystemClock.elapsedRealtime()-started);long speed=done*1000/elapsed;long remain=speed==0?0:Math.max(0,total-done)/speed;ui.post(()->progress.setText(name+"\n"+size(done)+" / "+size(total)+" · "+size(speed)+"/s · 約 "+remain+" 秒"));}
    private void uploadAll(ArrayList<Uri> uris){if(uris.isEmpty()||!beginTransfer())return;RoomClient c=client;transfers.execute(()->{try{int count=0;for(Uri uri:uris){if(cancelled)throw new IOException("已取消傳輸");upload(c,uri);count++;}notice("✓ "+count+" 個檔案已加入電腦共享籃框");refresh();}catch(Exception e){if(cancelled)notice("已取消傳輸");else failure(e);}finally{endTransfer();}});}
    private void upload(RoomClient c,Uri uri)throws Exception{
        if(!"content".equals(uri.getScheme()))throw new IOException("請透過系統檔案選擇器加入檔案");String name=null;long length=-1;
        try(Cursor cursor=getContentResolver().query(uri,new String[]{OpenableColumns.DISPLAY_NAME,OpenableColumns.SIZE},null,null,null)){if(cursor!=null&&cursor.moveToFirst()){int ni=cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME),si=cursor.getColumnIndex(OpenableColumns.SIZE);if(ni>=0)name=cursor.getString(ni);if(si>=0&&!cursor.isNull(si))length=cursor.getLong(si);}}
        if(!LanRules.safeName(name))throw new IOException("此檔名無法安全儲存，請先重新命名");if(length<0)throw new IOException("無法得知檔案大小，請先將檔案存到手機再分享");if(length>16L*1024*1024*1024)throw new IOException("單檔最多 16 GiB");final long total=length;final String filename=name;
        RequestBody body=new RequestBody(){public MediaType contentType(){return MediaType.get("application/octet-stream");}public long contentLength(){return total;}public void writeTo(BufferedSink sink)throws IOException{long done=0,started=SystemClock.elapsedRealtime(),last=0;try(InputStream input=getContentResolver().openInputStream(uri)){if(input==null)throw new IOException("無法讀取檔案");byte[] buffer=new byte[65536];int n;while((n=input.read(buffer))!=-1){if(cancelled)throw new IOException("已取消");sink.write(buffer,0,n);done+=n;long now=SystemClock.elapsedRealtime();if(now-last>250){updateProgress(filename,done,total,started);last=now;}}if(done!=total)throw new IOException("來源檔案大小已變更");}}};
        HttpUrl url=Objects.requireNonNull(HttpUrl.parse(c.endpoint+"/v1/files")).newBuilder().addQueryParameter("name",filename).build();transfer=c.http.newCall(c.request("/v1/files").url(url).header("x-file-size",Long.toString(total)).put(body).build());try(Response response=transfer.execute()){RoomClient.check(response);}
    }
    private void download(JSONObject item){if(!beginTransfer())return;RoomClient c=client;transfers.execute(()->{Uri target=null;File legacy=null;boolean complete=false;try{
        String name=item.getString("name");if(!LanRules.safeName(name))throw new IOException("不安全的檔名");String id=item.getString("file_id");UUID.fromString(id);long expected=item.getLong("size");if(expected<0||expected>16L*1024*1024*1024)throw new IOException("檔案大小超過限制");
        transfer=c.http.newCall(c.request("/v1/files/"+id).build());try(Response response=transfer.execute()){RoomClient.check(response);if(response.body()==null || response.body().contentLength()!=expected)throw new IOException("檔案大小不符");
            if(Build.VERSION.SDK_INT>=29){ContentValues v=new ContentValues();v.put(MediaStore.Downloads.DISPLAY_NAME,name);v.put(MediaStore.Downloads.MIME_TYPE,"application/octet-stream");v.put(MediaStore.Downloads.RELATIVE_PATH,Environment.DIRECTORY_DOWNLOADS+"/PocketDrop");v.put(MediaStore.Downloads.IS_PENDING,1);target=getContentResolver().insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI,v);if(target==null)throw new IOException("無法建立下載檔案");}
            else{File dir=new File(getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS),"PocketDrop/"+UUID.randomUUID());if(!dir.mkdirs())throw new IOException("無法建立資料夾");legacy=new File(dir,name);target=FileProvider.getUriForFile(this,getPackageName()+".files",legacy);}
            MessageDigest digest=MessageDigest.getInstance("SHA-256");long done=0,started=SystemClock.elapsedRealtime(),last=0;
            try(InputStream in=response.body().byteStream();OutputStream out=Build.VERSION.SDK_INT>=29?getContentResolver().openOutputStream(target):new FileOutputStream(legacy)){if(out==null)throw new IOException("無法儲存檔案");byte[] buffer=new byte[65536];int n;while((n=in.read(buffer))!=-1){if(cancelled)throw new IOException("已取消");done+=n;if(done>expected)throw new IOException("內容超出宣告大小");out.write(buffer,0,n);digest.update(buffer,0,n);long now=SystemClock.elapsedRealtime();if(now-last>250){updateProgress(name,done,expected,started);last=now;}}}
            if(done!=expected)throw new IOException("下載不完整");String checksum=response.header("x-content-sha256");if(checksum!=null&&!checksum.equals(RoomClient.hex(digest.digest())))throw new IOException("完整性驗證失敗，已移除下載檔案");if(cancelled)throw new IOException("已取消");
            if(Build.VERSION.SDK_INT>=29){ContentValues values=new ContentValues();values.put(MediaStore.Downloads.IS_PENDING,0);getContentResolver().update(target,values,null,null);}complete=true;Uri saved=target;
            ui.post(()->{progress.setText("✓ 下載完成"+(checksum==null?" · 來源雜湊計算中，尚未比對":" · SHA-256 已驗證"));new AlertDialog.Builder(this).setTitle("下載完成").setMessage(name+"\n"+(Build.VERSION.SDK_INT>=29?"已存入 Downloads/PocketDrop":"已存入 PocketDrop 的 App 下載資料夾")).setNegativeButton("完成",null).setPositiveButton("開啟",(d,w)->openFile(saved,name)).show();});
        }
    }catch(Exception e){if(cancelled)notice("已取消下載");else failure(e);}finally{if(!complete){if(legacy!=null){if(!legacy.delete())notice("下載中斷，請檢查儲存空間");}else if(target!=null)getContentResolver().delete(target,null,null);}endTransfer();}});}
    private void openFile(Uri uri,String name){String ext=name.contains(".")?name.substring(name.lastIndexOf('.')+1).toLowerCase(Locale.ROOT):"";if(Arrays.asList("apk","exe","msi","bat","cmd","ps1","com","scr").contains(ext)){notice("此類檔案僅供儲存，請自行到檔案管理器處理");return;}String mime=android.webkit.MimeTypeMap.getSingleton().getMimeTypeFromExtension(ext);try{startActivity(new Intent(Intent.ACTION_VIEW).setDataAndType(uri,mime==null?"application/octet-stream":mime).addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION));}catch(ActivityNotFoundException e){notice("手機沒有可開啟此格式的 App，檔案已保存");}}
}
