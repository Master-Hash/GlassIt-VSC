use std::mem::size_of;

use napi_derive::napi;
use windows::{
    Win32::{
        Foundation::{COLORREF, CloseHandle, HANDLE, HWND, LPARAM},
        System::{
            Diagnostics::ToolHelp::{
                CreateToolhelp32Snapshot, PROCESSENTRY32W, Process32FirstW, Process32NextW,
                TH32CS_SNAPPROCESS,
            },
            Threading::{
                OpenProcess, PROCESS_NAME_WIN32, PROCESS_QUERY_LIMITED_INFORMATION,
                QueryFullProcessImageNameW,
            },
        },
        UI::WindowsAndMessaging::{
            EnumThreadWindows, EnumWindows, GWL_EXSTYLE, GetWindowLongPtrW,
            GetWindowThreadProcessId, IsWindowVisible, LWA_ALPHA, SetLayeredWindowAttributes,
            SetWindowLongPtrW, WS_EX_LAYERED,
        },
    },
    core::{BOOL, PWSTR},
};

struct OwnedHandle(HANDLE);

impl OwnedHandle {
    fn new(handle: HANDLE) -> Self {
        Self(handle)
    }

    fn get(&self) -> HANDLE {
        self.0
    }
}

impl Drop for OwnedHandle {
    fn drop(&mut self) {
        unsafe {
            let _ = CloseHandle(self.0);
        }
    }
}

#[napi]
pub fn set_transparency(pid: u32, alpha: u8) -> bool {
    let Some(executable_path) = process_image_path(pid) else {
        return false;
    };

    let mut matched_window = false;
    let mut success = true;

    for process_pid in matching_process_ids(&executable_path) {
        let Some(main_window) = main_window_handle(process_pid) else {
            continue;
        };

        let thread_id = unsafe { GetWindowThreadProcessId(main_window, None) };
        if thread_id == 0 {
            success = false;
            continue;
        }

        let mut context = AlphaContext {
            alpha,
            success: true,
        };

        matched_window = true;
        let enum_result = unsafe {
            EnumThreadWindows(
                thread_id,
                Some(set_window_alpha_callback),
                LPARAM(&mut context as *mut AlphaContext as isize),
            )
        };

        success &= enum_result.as_bool() && context.success;
    }

    matched_window && success
}

fn matching_process_ids(executable_path: &str) -> Vec<u32> {
    let Ok(snapshot) = (unsafe { CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0) }) else {
        return Vec::new();
    };
    let snapshot = OwnedHandle::new(snapshot);

    let mut entry = PROCESSENTRY32W {
        dwSize: size_of::<PROCESSENTRY32W>() as u32,
        ..Default::default()
    };

    if unsafe { Process32FirstW(snapshot.get(), &mut entry) }.is_err() {
        return Vec::new();
    }

    let mut pids = Vec::new();
    loop {
        let process_pid = entry.th32ProcessID;
        if process_image_path(process_pid)
            .as_deref()
            .is_some_and(|path| path.eq_ignore_ascii_case(executable_path))
        {
            pids.push(process_pid);
        }

        if unsafe { Process32NextW(snapshot.get(), &mut entry) }.is_err() {
            break;
        }
    }

    pids
}

fn process_image_path(pid: u32) -> Option<String> {
    let handle = unsafe { OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid) }.ok()?;
    let handle = OwnedHandle::new(handle);

    let mut buffer = vec![0u16; 32_768];
    let mut size = buffer.len() as u32;

    unsafe {
        QueryFullProcessImageNameW(
            handle.get(),
            PROCESS_NAME_WIN32,
            PWSTR(buffer.as_mut_ptr()),
            &mut size,
        )
    }
    .ok()?;

    if size == 0 {
        return None;
    }

    buffer.truncate(size as usize);
    String::from_utf16(&buffer).ok()
}

struct MainWindowContext {
    pid: u32,
    hwnd: Option<HWND>,
}

fn main_window_handle(pid: u32) -> Option<HWND> {
    let mut context = MainWindowContext { pid, hwnd: None };

    let _ = unsafe {
        EnumWindows(
            Some(find_main_window_callback),
            LPARAM(&mut context as *mut MainWindowContext as isize),
        )
    };

    context.hwnd
}

extern "system" fn find_main_window_callback(hwnd: HWND, lparam: LPARAM) -> BOOL {
    let context = unsafe { &mut *(lparam.0 as *mut MainWindowContext) };

    if !unsafe { IsWindowVisible(hwnd) }.as_bool() {
        return true.into();
    }

    let mut window_pid = 0;
    unsafe {
        GetWindowThreadProcessId(hwnd, Some(&mut window_pid));
    }

    if window_pid == context.pid {
        context.hwnd = Some(hwnd);
        return false.into();
    }

    true.into()
}

struct AlphaContext {
    alpha: u8,
    success: bool,
}

extern "system" fn set_window_alpha_callback(hwnd: HWND, lparam: LPARAM) -> BOOL {
    let context = unsafe { &mut *(lparam.0 as *mut AlphaContext) };

    if !set_window_alpha(hwnd, context.alpha) {
        context.success = false;
        return false.into();
    }

    true.into()
}

fn set_window_alpha(hwnd: HWND, alpha: u8) -> bool {
    if !unsafe { IsWindowVisible(hwnd) }.as_bool() {
        return true;
    }

    let window_long = unsafe { GetWindowLongPtrW(hwnd, GWL_EXSTYLE) };
    unsafe {
        SetWindowLongPtrW(hwnd, GWL_EXSTYLE, window_long | WS_EX_LAYERED.0 as isize);

        SetLayeredWindowAttributes(hwnd, COLORREF(0), alpha, LWA_ALPHA).is_ok()
    }
}
