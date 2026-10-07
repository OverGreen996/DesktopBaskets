//! Small native host adapter; no Tauri, WebView2 or window runtime.
use serde::Serialize;
use std::path::PathBuf;
use tokio::sync::mpsc::UnboundedSender;

pub type State<'a, T> = &'a T;
#[derive(Clone)]
pub struct AppHandle {
    pub data: PathBuf,
    pub downloads: PathBuf,
    pub events: UnboundedSender<(String, serde_json::Value)>,
}
impl AppHandle {
    pub fn emit<T: Serialize>(&self, name: &str, value: T) -> Result<(), String> {
        self.events.send((name.into(), serde_json::to_value(value).map_err(|e| e.to_string())?))
            .map_err(|_| "Host disconnected".into())
    }
    pub fn path(&self) -> &Self { self }
    pub fn app_data_dir(&self) -> Result<PathBuf, String> { Ok(self.data.clone()) }
    pub fn download_dir(&self) -> Result<PathBuf, String> { Ok(self.downloads.clone()) }
}
pub mod async_runtime { pub use tokio::spawn; }
