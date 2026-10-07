#![allow(dead_code)]
mod host;
mod phone;
use serde_json::{json, Value};
use std::{path::PathBuf, sync::{Arc, Mutex}, io::{BufRead, Read, Write}};
use tokio::sync::{mpsc, Semaphore};

fn output(value: Value) {
    // Serialize complete records under one lock, including transfer events.
    let stdout = std::io::stdout();
    let mut writer = stdout.lock();
    if let Ok(line) = serde_json::to_string(&value) { let _ = writeln!(writer, "{line}"); let _ = writer.flush(); }
}
fn argument(args: &[String], name: &str) -> Option<String> {
    args.windows(2).find(|v| v[0] == name).map(|v| v[1].clone())
}
fn text(v: &Value, key: &str) -> Result<String, String> {
    v[key].as_str().map(str::to_owned).ok_or_else(|| format!("Missing {key}"))
}
async fn status(state: &phone::Phone) -> Result<Value, String> {
    let snapshot=phone::phone_snapshot(state).await?;
    let core=phone::current(state)?;
    let mut paths=core.local_paths()?;
    // Room IDs differ from the original source IDs. Older PocketDrop hosts expose
    // an authenticated owner prefix; only an unambiguous exact local record maps.
    let local=core.local_snapshot()?;
    let origin=format!("電腦 {}",&core.device_id[..8]);
    for remote in &snapshot.files {
        if remote.origin!=origin {continue}
        let candidates=local.files.iter().filter(|f|f.name==remote.name&&f.size==remote.size&&f.sha256==remote.sha256).collect::<Vec<_>>();
        if candidates.len()==1 {
            if let Some(path)=paths.get(&candidates[0].file_id).cloned(){paths.insert(remote.file_id.clone(),path);}
        }
    }
    Ok(json!({"snapshot":snapshot,"mode":phone::peer::room_mode(state)?,"self_id":core.device_id,"local_paths":paths}))
}
fn interfaces() -> Result<Value, String> {
    Ok(json!(if_addrs::get_if_addrs().map_err(|_|"無法列出網路介面")?.into_iter().filter_map(|i| {
        match i.ip() { std::net::IpAddr::V4(ip) if phone::local_ip(ip)=>Some(json!({"name":i.name,"address":ip.to_string()})), _=>None }
    }).collect::<Vec<_>>()))
}
async fn dispatch(app: host::AppHandle, state: Arc<phone::Phone>, command: &Value) -> Result<Value, String> {
    match command["op"].as_str().unwrap_or("") {
        "interfaces"=>interfaces(),
        "start"=>{phone::phone_start(app,&state,text(command,"address")?).await?;status(&state).await},
        "snapshot"=>status(&state).await,
        "text"=>{phone::phone_text(&state,text(command,"content")?).await?;status(&state).await},
        "invite"=>serde_json::to_value(phone::phone_invite(&state)?).map_err(|e|e.to_string()),
        "nearby"=>serde_json::to_value(phone::peer::nearby_rooms(&state).await?).map_err(|e|e.to_string()),
        "join_code"=>{phone::peer::join_code(&state,text(command,"address")?,text(command,"code")?).await?;status(&state).await},
        "join_qr"=>{phone::peer::join_qr(&state,text(command,"qr")?).await?;status(&state).await},
        "leave"=>{phone::peer::leave_room(&state)?;status(&state).await},
        "files_add"=>{
            let paths=command["paths"].as_array().ok_or("請選擇檔案")?;
            if paths.len()>100 {return Err("每次最多加入 100 個檔案".into())}
            let core=phone::current(&state)?;
            let mut accepted=vec![]; let mut rejected=vec![];
            for value in paths {
                let path=PathBuf::from(value.as_str().ok_or("無效路徑")?);
                match core.add_file(path.clone(),"Desktop Baskets") {
                    Ok(())=>accepted.push(path.to_string_lossy().to_string()),
                    Err(error)=>rejected.push(json!({"name":path.file_name().unwrap_or_default().to_string_lossy(),"error":error})),
                }
            }
            if let Some(profile)=core.joined_profile() {
                phone::peer::heartbeat(&core,&profile).await?;
                core.set_cached(profile.snapshot().await?);
            }
            Ok(json!({"accepted":accepted,"rejected":rejected,"state":status(&state).await?}))
        },
        "remove"=>{phone::phone_remove(&state,text(command,"file_id")?)?;status(&state).await},
        "unshare_paths"=>{
            let values=command["paths"].as_array().ok_or("請選擇本機檔案")?;
            if values.len()>100{return Err("每次最多處理 100 個檔案".into())}
            let paths=values.iter().map(|v|v.as_str().map(str::to_owned).ok_or("無效路徑")).collect::<Result<Vec<_>,_>>()?;
            let core=phone::current(&state)?;core.unshare_paths(paths)?;
            if let Some(profile)=core.joined_profile(){phone::peer::heartbeat(&core,&profile).await?;core.set_cached(profile.snapshot().await?);}
            status(&state).await
        },
        "revoke"=>{phone::phone_revoke(&state,text(command,"device_id")?)?;status(&state).await},
        "download"=>Ok(json!({"path":phone::peer::download_file(app,&state,text(command,"file_id")?).await?})),
        "cancel"=>{phone::peer::cancel_download(&state)?;Ok(Value::Null)},
        _=>Err("未知的共享操作".into()),
    }
}
#[tokio::main(flavor="multi_thread",worker_threads=2)]
async fn main() {
    let args=std::env::args().collect::<Vec<_>>();
    let Some(data)=argument(&args,"--data") else { eprintln!("--data required");return };
    let Some(downloads)=argument(&args,"--downloads") else {eprintln!("--downloads required");return};
    let (events,mut receiver)=mpsc::unbounded_channel();
    let app=host::AppHandle{data:PathBuf::from(data),downloads:PathBuf::from(downloads),events};
    let state=Arc::new(phone::Phone(Mutex::new(None)));
    let event_state=state.clone();
    tokio::spawn(async move {
        let mut previous=Value::Null; let mut online=Value::Null;
        while let Some((name,payload))=receiver.recv().await {
            if name=="phone-changed" {
                if let Ok(current)=status(&event_state).await {
                    if current!=previous {previous=current.clone();output(json!({"event":"state","payload":current}));}
                }
            } else if name=="room-online" {
                if payload!=online {online=payload.clone();output(json!({"event":name,"payload":payload}));}
            } else {output(json!({"event":name,"payload":payload}));}
        }
    });
    let (commands,mut requests)=mpsc::channel::<String>(32);
    std::thread::spawn(move || {
        // A bounded line prevents accidental/untrusted host messages from consuming unlimited memory.
        let mut reader=std::io::stdin().lock();
        loop {
            let mut bytes=Vec::new();
            if std::io::Read::by_ref(&mut reader).take(1_048_577).read_until(b'\n',&mut bytes).unwrap_or(0)==0 {break}
            if bytes.len()>1_048_576 {break}
            let Ok(line)=String::from_utf8(bytes) else {break};
            if commands.blocking_send(line).is_err(){break}
        }
    });
    let gate=Arc::new(Semaphore::new(16));
    while let Some(line)=requests.recv().await {
        let Ok(command)=serde_json::from_str::<Value>(&line) else {continue};
        if command["op"]=="shutdown" {break}
        let permit=gate.clone().acquire_owned().await.unwrap();
        let app=app.clone();let state=state.clone();
        tokio::spawn(async move {
            let _permit=permit;
            let id=command["id"].clone();
            let result=dispatch(app,state,&command).await;
            match result {Ok(value)=>output(json!({"id":id,"ok":true,"result":value})),Err(error)=>output(json!({"id":id,"ok":false,"error":error}))}
        });
    }
    // stdin closes with the owner. Tokio cancels bounded background/transfer work on exit.
}
