NAME = HeapShot.Reader
TARGET = $(NAME).dll
CSPROJ = $(NAME).csproj
XBUILD = xbuild
XBUILD_ARGS = "/property:Configuration=Debug" $(CSPROJ)

CSFILES = $(wildcard *.cs)

$(TARGET): $(CSFILES) $(CSPROJ)
	$(XBUILD) $(XBUILD_ARGS)

all: $(TARGET)

install-data-local: $(TARGET)
	$(mkinstalldirs) $(DESTDIR)$(pkglibdir)
	$(INSTALL_DATA) $(TARGET) $(DESTDIR)$(pkglibdir)

uninstall-local:
	cd $(DESTDIR)$(pkglibdir) && rm -f $(TARGET)

EXTRA_DIST = 			\
	$(CSFILES)

CLEANFILES =			\
	$(TARGET)		\
	$(TARGET).mdb

clean-local:
	$(XBUILD) $(XBUILD_ARGS) /t:Clean
	rm -rf bin obj

