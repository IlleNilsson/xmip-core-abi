//! Where the runtime's native library is, by one rule for every surface
//! (ADR-0052 clause 1): what the surface was told — a configuration key, a
//! command-line flag — else the `XMIP_RUNTIME_LIBRARY` environment variable,
//! else the library beside the executable.
//!
//! `dotnet/Xmip.Surface/RuntimeLibrary.cs` is this rule for the .NET
//! surfaces. This is the same rule for the Rust ones, kept here rather than in
//! the language server so that no surface declares a copy (ADR-0044, read for
//! the surfaces: shared code lives where both already depend). Two copies of a
//! discovery rule drift at the platform table first, and an operator who set
//! the variable once per machine would find one surface honoring it and
//! another not.

use std::path::{Path, PathBuf};

/// The environment variable an operator sets once per machine.
pub const ENVIRONMENT_VARIABLE: &str = "XMIP_RUNTIME_LIBRARY";

/// The library's file name on this platform: what `cargo build` in
/// `xmip-core-runtime` leaves, as the linker names a cdylib.
#[must_use]
pub fn file_name() -> &'static str {
    if cfg!(target_os = "windows") {
        "xmip_core_runtime.dll"
    } else if cfg!(target_os = "macos") {
        "libxmip_core_runtime.dylib"
    } else {
        "libxmip_core_runtime.so"
    }
}

/// The last resort: the library next to whatever binary this process is.
/// Only the file name when the executable's own location cannot be read, so
/// the path still says what was looked for.
#[must_use]
pub fn beside_executable() -> PathBuf {
    std::env::current_exe()
        .ok()
        .and_then(|exe| exe.parent().map(Path::to_path_buf))
        .unwrap_or_default()
        .join(file_name())
}

/// The rule itself, with every input in hand: what the surface was told, else
/// the environment, else beside the executable.
///
/// A blank value from either of the first two is nothing said, not a path: a
/// shell exports an empty variable as easily as an unset one, and a
/// configuration key left empty means the same as one left out.
#[must_use]
pub fn choose(
    told: Option<PathBuf>,
    from_environment: Option<&str>,
    beside_executable: PathBuf,
) -> PathBuf {
    told.filter(|path| !path.as_os_str().to_string_lossy().trim().is_empty())
        .or_else(|| {
            from_environment
                .map(str::trim)
                .filter(|value| !value.is_empty())
                .map(PathBuf::from)
        })
        .unwrap_or(beside_executable)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn the_file_name_is_what_the_linker_leaves_on_this_platform() {
        let name = file_name();

        assert!(name.contains("xmip_core_runtime"));
        if cfg!(target_os = "windows") {
            assert_eq!(name, "xmip_core_runtime.dll");
        } else {
            assert!(name.starts_with("lib"));
        }
    }

    #[test]
    fn the_last_resort_is_the_library_beside_the_binary() {
        let path = beside_executable();

        assert_eq!(path.file_name().expect("a name"), file_name());
        assert!(path.parent().is_some());
    }

    #[test]
    fn what_the_surface_was_told_wins_then_the_environment_then_beside() {
        let beside = PathBuf::from("C:/bin").join(file_name());

        assert_eq!(
            choose(Some("C:/a.dll".into()), Some("C:/b.dll"), beside.clone()),
            PathBuf::from("C:/a.dll")
        );
        assert_eq!(
            choose(None, Some("C:/b.dll"), beside.clone()),
            PathBuf::from("C:/b.dll")
        );
        assert_eq!(choose(None, None, beside.clone()), beside);
    }

    #[test]
    fn a_blank_value_is_nothing_said() {
        let beside = PathBuf::from("C:/bin").join(file_name());

        assert_eq!(
            choose(Some(PathBuf::new()), Some("  "), beside.clone()),
            beside
        );
        assert_eq!(
            choose(Some(PathBuf::from(" ")), Some("C:/b.dll"), beside),
            PathBuf::from("C:/b.dll")
        );
    }
}
