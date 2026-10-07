package local.pocketdrop.android;
import org.junit.Test;
import org.junit.Assume;
import static org.junit.Assert.*;
import org.json.JSONObject;
import java.nio.file.*;
import java.util.*;
import okhttp3.*;

/** Runs the production Java SRP/TLS client against the built Windows server. */
public class CodePairingInteropTest {
 private final Map<Process,java.io.BufferedReader> readers=new IdentityHashMap<>();
 private long next;
 private JSONObject call(Process p,String op,JSONObject args)throws Exception{
  long id=++next;args.put("id",id).put("op",op);p.getOutputStream().write((args.toString()+"\n").getBytes(java.nio.charset.StandardCharsets.UTF_8));p.getOutputStream().flush();
  java.io.BufferedReader reader=readers.get(p);String line;while((line=reader.readLine())!=null){JSONObject value=new JSONObject(line);if(value.optLong("id",-1)==id){if(!value.optBoolean("ok"))throw new java.io.IOException(value.optString("error"));return value;}}
  throw new java.io.IOException("Native sharing core exited");
 }
 private Process start(String exe,Path dir)throws Exception{
  Process p=new ProcessBuilder(exe,"--data",dir.resolve("room").toString(),"--downloads",dir.resolve("downloads").toString()).redirectError(dir.resolve("server.log").toFile()).start();
  readers.put(p,new java.io.BufferedReader(new java.io.InputStreamReader(p.getInputStream(),java.nio.charset.StandardCharsets.UTF_8)));
  try{org.json.JSONArray interfaces=call(p,"interfaces",new JSONObject()).getJSONArray("result");if(interfaces.length()==0)throw new java.io.IOException("Private LAN interface required");call(p,"start",new JSONObject().put("address",interfaces.getJSONObject(0).getString("address")));
   Files.write(dir.resolve("invite.json"),call(p,"invite",new JSONObject()).getJSONObject("result").toString().getBytes(java.nio.charset.StandardCharsets.UTF_8));return p;
  }catch(Exception e){p.destroyForcibly();throw e;}
 }
 private JSONObject invite(Path dir)throws Exception{Path p=dir.resolve("invite.json");for(int i=0;i<200;i++){if(Files.exists(p))try{return new JSONObject(new String(Files.readAllBytes(p),java.nio.charset.StandardCharsets.UTF_8));}catch(Exception ignored){}Thread.sleep(100);}throw new IllegalStateException("Windows server did not start");}
 @Test public void androidCodePairingAgainstWindowsAndColdReconnect()throws Exception{
  String exe=System.getenv("PD_INTEROP_EXE");Assume.assumeTrue(exe!=null&&Files.isRegularFile(Path.of(exe)));
  Path dir=Files.createTempDirectory("pd-java-interop-");Process process=start(exe,dir);
  try{JSONObject invitation=invite(dir),qr=new JSONObject(invitation.getString("qr"));String endpoint=qr.getString("endpoint"),code=invitation.getString("code");
   String publicKey=Base64.getEncoder().encodeToString(new byte[64]),id=UUID.randomUUID().toString();
   try{CodePairing.pair(endpoint,code.equals("00000000")?"11111111":"00000000",id,"Android 測試",publicKey);fail("wrong code accepted");}catch(RoomClient.ApiException expected){assertEquals(401,expected.status);}
   RoomClient paired=CodePairing.pair(endpoint,code,id,"Android 測試",publicKey);assertEquals(qr.getString("room_id"),paired.profile.getString("room_id"));
   paired.text("Android to PC via code");assertEquals("Android to PC via code",paired.state().getString("text"));
   try{CodePairing.pair(endpoint,code,UUID.randomUUID().toString(),"replay",publicKey);fail("used invite accepted");}catch(RoomClient.ApiException expected){assertEquals(401,expected.status);}
   JSONObject saved=new JSONObject(paired.profile.toString());
   for(int restart=0;restart<3;restart++){
    process.destroyForcibly();process.waitFor();
    try{paired.state();fail("stopped server appeared online");}catch(java.io.IOException expected){}
    Files.delete(dir.resolve("invite.json"));process=start(exe,dir);
    JSONObject next=new JSONObject(invite(dir).getString("qr"));
    assertEquals("restart must retain the phone's saved endpoint",saved.getString("endpoint"),next.getString("endpoint"));
    // Do not inject the new endpoint. Exercise the very same running phone client,
    // then reconstruct it from the original persisted profile, without pairing.
    assertEquals("Android to PC via code",paired.state().getString("text"));
    RoomClient reopened=new RoomClient(new JSONObject(saved.toString()));
    assertEquals("Android to PC via code",reopened.state().getString("text"));
    assertEquals(paired.profile.getString("credential"),reopened.profile.getString("credential"));
   }
  }finally{process.destroyForcibly();process.waitFor();try(var paths=Files.walk(dir)){for(Path p:paths.sorted(Comparator.reverseOrder()).toList())Files.deleteIfExists(p);}}
 }
}
